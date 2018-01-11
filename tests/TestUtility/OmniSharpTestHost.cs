using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp;
using OmniSharp.Cake;
using OmniSharp.DotNet;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Eventing;
using OmniSharp.Mef;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.MSBuild;
using OmniSharp.Roslyn.CSharp.Services;
using OmniSharp.Services;
using OmniSharp.Utilities;
using TestUtility.Logging;
using Xunit.Abstractions;

namespace TestUtility
{
    public class OmniSharpTestHost : DisposableObject
    {
        private const string MSBuildSDKsPath = "MSBuildSDKsPath";

        private static Lazy<Assembly[]> s_lazyAssemblies = new Lazy<Assembly[]>(() => new[]
        {
            typeof(OmniSharpEndpoints).GetTypeInfo().Assembly, // OmniSharp.Abstractions
            typeof(HostHelpers).GetTypeInfo().Assembly, // OmniSharp.Host
            typeof(DotNetProjectSystem).GetTypeInfo().Assembly, // OmniSharp.DotNet
            typeof(RunTestRequest).GetTypeInfo().Assembly, // OmniSharp.DotNetTest
            typeof(ProjectSystem).GetTypeInfo().Assembly, // OmniSharp.MSBuild
            typeof(OmniSharpWorkspace).GetTypeInfo().Assembly, // OmniSharp.Roslyn
            typeof(RoslynFeaturesHostServicesProvider).GetTypeInfo().Assembly, // OmniSharp.Roslyn.CSharp
            typeof(CakeProjectSystem).GetTypeInfo().Assembly, // OmniSharp.Cake
        });

        private readonly TestServiceProvider _serviceProvider;
        private readonly CompositionHost _compositionHost;
        private readonly string _oldMSBuildSdksPath;

        private Dictionary<(string name, string language), Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> _handlers;

        public ILoggerFactory LoggerFactory { get; }
        public OmniSharpWorkspace Workspace { get; }

        private OmniSharpTestHost(
            TestServiceProvider serviceProvider,
            ILoggerFactory loggerFactory,
            OmniSharpWorkspace workspace,
            CompositionHost compositionHost,
            string oldMSBuildSdksPath)
        {
            this._serviceProvider = serviceProvider;
            this._compositionHost = compositionHost;

            this.LoggerFactory = loggerFactory;
            this.Workspace = workspace;

            _oldMSBuildSdksPath = oldMSBuildSdksPath;
        }

        ~OmniSharpTestHost()
        {
            throw new InvalidOperationException($"{nameof(OmniSharpTestHost)}.{nameof(Dispose)}() not called.");
        }

        protected override void DisposeCore(bool disposing)
        {
            Environment.SetEnvironmentVariable(MSBuildSDKsPath, _oldMSBuildSdksPath);

            this._serviceProvider.Dispose();
            this._compositionHost.Dispose();

            this.LoggerFactory.Dispose();
            this.Workspace.Dispose();
        }

        private static string GetDotNetCliFolderName(DotNetCliVersion dotNetCliVersion)
        {
            switch (dotNetCliVersion)
            {
                case DotNetCliVersion.Current: return ".dotnet";
                case DotNetCliVersion.Legacy: return ".dotnet-legacy";
                case DotNetCliVersion.Future: throw new InvalidOperationException("Test infrastructure does not support a future .NET Core SDK yet.");
                default: throw new ArgumentException($"Unknown {nameof(dotNetCliVersion)}: {dotNetCliVersion}", nameof(dotNetCliVersion));
            }
        }

        public static OmniSharpTestHost Create(string path = null, ITestOutputHelper testOutput = null, IEnumerable<KeyValuePair<string, string>> configurationData = null, DotNetCliVersion dotNetCliVersion = DotNetCliVersion.Current)
        {
            var dotNetPath = Path.Combine(
                TestAssets.Instance.RootFolder,
                GetDotNetCliFolderName(dotNetCliVersion),
                "dotnet");

            if (!File.Exists(dotNetPath))
            {
                dotNetPath = Path.ChangeExtension(dotNetPath, ".exe");
            }

            if (!File.Exists(dotNetPath))
            {
                throw new InvalidOperationException($"Local .NET CLI path does not exist. Did you run build.(ps1|sh) from the command line?");
            }

            var builder = new Microsoft.Extensions.Configuration.ConfigurationBuilder();
            builder.AddInMemoryCollection(configurationData);
            var configuration = builder.Build();

            var environment = new OmniSharpEnvironment(path, logLevel: LogLevel.Trace);
            var loggerFactory = new LoggerFactory().AddXunit(testOutput);
            var sharedTextWriter = new TestSharedTextWriter(testOutput);

            var serviceProvider = new TestServiceProvider(environment, loggerFactory, sharedTextWriter, configuration);

            var compositionHost = new CompositionHostBuilder(serviceProvider, environment, NullEventEmitter.Instance)
                .WithAssemblies(s_lazyAssemblies.Value)
                .Build();

            var workspace = compositionHost.GetExport<OmniSharpWorkspace>();
            var logger = loggerFactory.CreateLogger<OmniSharpTestHost>();

            var dotNetCli = compositionHost.GetExport<DotNetCliService>();
            dotNetCli.SetDotNetPath(dotNetPath);
            var version = dotNetCli.GetVersion();
            logger.LogInformation($"Using .NET CLI: {version}");

            var oldMSBuildSdksPath = SetMSBuildSdksPath(dotNetCli);

            WorkspaceInitializer.Initialize(serviceProvider, compositionHost, configuration, logger);

            var host = new OmniSharpTestHost(serviceProvider, loggerFactory, workspace, compositionHost, oldMSBuildSdksPath);

            // Force workspace to be updated
            var service = host.GetWorkspaceInformationService();
            service.Handle(new WorkspaceInformationRequest()).Wait();

            return host;
        }

        private static string SetMSBuildSdksPath(DotNetCliService dotNetCli)
        {
            var oldMSBuildSDKsPath = Environment.GetEnvironmentVariable(MSBuildSDKsPath);

            var info = dotNetCli.GetInfo();
            var msbuildSdksPath = Path.Combine(info.BasePath, "Sdks");

            if (Directory.Exists(msbuildSdksPath))
            {
                Environment.SetEnvironmentVariable(MSBuildSDKsPath, msbuildSdksPath);
            }

            return oldMSBuildSDKsPath;
        }

        public T GetExport<T>()
        {
            return this._compositionHost.GetExport<T>();
        }

        public THandler GetRequestHandler<THandler>(string name, string languageName = LanguageNames.CSharp) where THandler : IRequestHandler
        {
            if (_handlers == null)
            {
                var exports = this._compositionHost.GetExports<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>>();
                _handlers = exports.ToDictionary(
                    keySelector: export => (export.Metadata.EndpointName, export.Metadata.Language),
                    elementSelector: export => export);
            }

            return (THandler)_handlers[(name, languageName)].Value;
        }

        public WorkspaceInformationService GetWorkspaceInformationService()
        {
            return GetRequestHandler<WorkspaceInformationService>(OmniSharpEndpoints.WorkspaceInformation, "Projects");
        }

        public void AddFilesToWorkspace(params TestFile[] testFiles)
        {
            TestHelpers.AddProjectToWorkspace(
                this.Workspace,
                "project.json",
                new[] { "dnx451", "dnxcore50" },
                testFiles.Where(f => f.FileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToArray());

            foreach (var csxFile in testFiles.Where(f => f.FileName.EndsWith(".csx", StringComparison.OrdinalIgnoreCase)))
            {
                TestHelpers.AddCsxProjectToWorkspace(Workspace, csxFile);
            }
        }
    }
}
