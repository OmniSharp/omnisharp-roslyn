using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Composition.Hosting.Core;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Script;
using OmniSharp.Services;
using OmniSharp.Utilities;
using TestUtility.Logging;
using Xunit.Abstractions;

namespace TestUtility
{
    public class OmniSharpTestHost : DisposableObject
    {
        private static Lazy<Assembly[]> s_lazyAssemblies = new Lazy<Assembly[]>(() => new[]
        {
            typeof(OmniSharpEndpoints).GetTypeInfo().Assembly, // OmniSharp.Abstractions
            typeof(HostHelpers).GetTypeInfo().Assembly, // OmniSharp.Host
            typeof(DotNetProjectSystem).GetTypeInfo().Assembly, // OmniSharp.DotNet
            typeof(RunTestRequest).GetTypeInfo().Assembly, // OmniSharp.DotNetTest
            typeof(ProjectSystem).GetTypeInfo().Assembly, // OmniSharp.MSBuild
            typeof(ScriptProjectSystem).GetTypeInfo().Assembly, // OmniSharp.Script
            typeof(OmniSharpWorkspace).GetTypeInfo().Assembly, // OmniSharp.Roslyn
            typeof(RoslynFeaturesHostServicesProvider).GetTypeInfo().Assembly, // OmniSharp.Roslyn.CSharp
            typeof(CakeProjectSystem).GetTypeInfo().Assembly, // OmniSharp.Cake
        });

        private readonly TestServiceProvider _serviceProvider;
        private readonly CompositionHost _compositionHost;

        private Dictionary<(string name, string language), Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> _handlers;

        public ILoggerFactory LoggerFactory { get; }
        public OmniSharpWorkspace Workspace { get; }
        public ILogger<OmniSharpTestHost> Logger { get; }

        private OmniSharpTestHost(
            TestServiceProvider serviceProvider,
            ILoggerFactory loggerFactory,
            OmniSharpWorkspace workspace,
            CompositionHost compositionHost)
        {
            _serviceProvider = serviceProvider;
            _compositionHost = compositionHost;

            this.LoggerFactory = loggerFactory;
            this.Workspace = workspace;
            this.Logger = loggerFactory.CreateLogger<OmniSharpTestHost>();
        }

        ~OmniSharpTestHost()
        {
            throw new InvalidOperationException($"{nameof(OmniSharpTestHost)}.{nameof(Dispose)}() not called.");
        }

        protected override void DisposeCore(bool disposing)
        {
            _serviceProvider.Dispose();
            _compositionHost.Dispose();

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

        public static OmniSharpTestHost Create(
            string path = null,
            ITestOutputHelper testOutput = null,
            IEnumerable<KeyValuePair<string, string>> configurationData = null,
            DotNetCliVersion dotNetCliVersion = DotNetCliVersion.Current,
            IEnumerable<ExportDescriptorProvider> additionalExports = null)
        {
            var environment = new OmniSharpEnvironment(path, logLevel: LogLevel.Trace);
            var loggerFactory = new LoggerFactory().AddXunit(testOutput);
            var logger = loggerFactory.CreateLogger<OmniSharpTestHost>();
            var sharedTextWriter = new TestSharedTextWriter(testOutput);

            var dotNetCliService = CreateTestDotNetCliService(dotNetCliVersion, loggerFactory);

            var info = dotNetCliService.GetInfo();
            logger.LogInformation($"Using .NET CLI: {info.Version}");

            var builder = new Microsoft.Extensions.Configuration.ConfigurationBuilder();
            builder.AddInMemoryCollection(configurationData);

            // We need to set the "UseLegacySdkResolver" for tests because
            // MSBuild's SDK resolver will not be able to locate the .NET Core SDKs
            // that we install locally in the ".dotnet" and ".dotnet-legacy" directories.
            // This property will cause the MSBuild project loader to set the
            // MSBuildSDKsPath environment variable to the correct path "Sdks" folder
            // within the appropriate .NET Core SDK.
            var msbuildProperties = new Dictionary<string, string>()
            {
                [$"MSBuild:{nameof(MSBuildOptions.UseLegacySdkResolver)}"] = "true",
                [$"MSBuild:{nameof(MSBuildOptions.MSBuildSDKsPath)}"] = Path.Combine(info.BasePath, "Sdks")
            };

            builder.AddInMemoryCollection(msbuildProperties);

            var configuration = builder.Build();

            var serviceProvider = new TestServiceProvider(environment, loggerFactory, sharedTextWriter, configuration, NullEventEmitter.Instance, dotNetCliService);

            var compositionHost = new CompositionHostBuilder(serviceProvider, s_lazyAssemblies.Value, additionalExports)
                .Build();

            var workspace = compositionHost.GetExport<OmniSharpWorkspace>();

            WorkspaceInitializer.Initialize(serviceProvider, compositionHost, configuration, logger);

            var host = new OmniSharpTestHost(serviceProvider, loggerFactory, workspace, compositionHost);

            // Force workspace to be updated
            var service = host.GetWorkspaceInformationService();
            service.Handle(new WorkspaceInformationRequest()).Wait();

            return host;
        }

        private static DotNetCliService CreateTestDotNetCliService(DotNetCliVersion dotNetCliVersion, ILoggerFactory loggerFactory)
        {
            var dotnetPath = Path.Combine(
                TestAssets.Instance.RootFolder,
                GetDotNetCliFolderName(dotNetCliVersion),
                "dotnet");

            if (!File.Exists(dotnetPath))
            {
                dotnetPath = Path.ChangeExtension(dotnetPath, ".exe");
            }

            if (!File.Exists(dotnetPath))
            {
                throw new InvalidOperationException($"Local .NET CLI path does not exist. Did you run build.(ps1|sh) from the command line?");
            }

            return new DotNetCliService(loggerFactory, NullEventEmitter.Instance, dotnetPath);
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

        public CodeCheckService GetCodeCheckServiceService()
        {
            return GetRequestHandler<CodeCheckService>(OmniSharpEndpoints.CodeCheck);
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

        public void ClearWorkspace()
        {
            var projectIds = Workspace.CurrentSolution.Projects.Select(x => x.Id);
            foreach (var projectId in projectIds)
            {
                Workspace.RemoveProject(projectId);
            }
        }

        public Task<TResponse> GetResponse<TRequest, TResponse>(
           string endpoint, TRequest request)
        {
            var service = GetRequestHandler<IRequestHandler<TRequest, TResponse>>(endpoint);
            return service.Handle(request);
        }
    }
}
