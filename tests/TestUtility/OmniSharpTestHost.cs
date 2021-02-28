using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Composition.Hosting.Core;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp;
using OmniSharp.Cake;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Eventing;
using OmniSharp.LanguageServerProtocol;
using OmniSharp.Mef;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.MSBuild;
using OmniSharp.Roslyn.CSharp.Services;
using OmniSharp.Script;
using OmniSharp.Services;
using OmniSharp.Utilities;
using Xunit.Abstractions;

namespace TestUtility
{
    public class OmniSharpTestHost : DisposableObject
    {
        private static Lazy<Assembly[]> s_lazyAssemblies = new Lazy<Assembly[]>(() => new[]
        {
            typeof(OmniSharpEndpoints).GetTypeInfo().Assembly, // OmniSharp.Abstractions
            typeof(HostHelpers).GetTypeInfo().Assembly, // OmniSharp.Host
            typeof(RunTestRequest).GetTypeInfo().Assembly, // OmniSharp.DotNetTest
            typeof(ProjectSystem).GetTypeInfo().Assembly, // OmniSharp.MSBuild
            typeof(ScriptProjectSystem).GetTypeInfo().Assembly, // OmniSharp.Script
            typeof(OmniSharpWorkspace).GetTypeInfo().Assembly, // OmniSharp.Roslyn
            typeof(RoslynFeaturesHostServicesProvider).GetTypeInfo().Assembly, // OmniSharp.Roslyn.CSharp
            typeof(OmniSharp.Roslyn.VisualBasic.Services.Structure.BlockStructureService).Assembly, // OmniSharp.Roslyn.VisualBasic
            typeof(CakeProjectSystem).GetTypeInfo().Assembly, // OmniSharp.Cake
            typeof(LanguageServerHost).Assembly, // OmniSharp.LanguageServerProtocol
        });

        private readonly IServiceProvider _serviceProvider;
        private readonly CompositionHost _compositionHost;

        private Dictionary<(string name, string language), Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> _handlers;
        private readonly string _originalCreatorToTrackDownMissedDisposes;

        public OmniSharpWorkspace Workspace { get; }
        public ILoggerFactory LoggerFactory { get; }
        public ILogger<OmniSharpTestHost> Logger { get; }
        public CompositionHost CompositionHost => _compositionHost;
        public IServiceProvider ServiceProvider => _serviceProvider;

        private OmniSharpTestHost(
            IServiceProvider serviceProvider,
            CompositionHost compositionHost,
            string originalCreatorToTrackDownMissedDisposes)
        {
            _serviceProvider = serviceProvider;
            _compositionHost = compositionHost;

            this.Workspace = compositionHost.GetExport<OmniSharpWorkspace>();
            this.LoggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
            this.Logger = this.LoggerFactory.CreateLogger<OmniSharpTestHost>();
            _originalCreatorToTrackDownMissedDisposes = originalCreatorToTrackDownMissedDisposes;
        }

        ~OmniSharpTestHost()
        {
            throw new InvalidOperationException($"{nameof(OmniSharpTestHost)}.{nameof(Dispose)}() not called, creation of object originated from {_originalCreatorToTrackDownMissedDisposes}.");
        }

        protected override void DisposeCore(bool disposing)
        {
            (_serviceProvider as IDisposable)?.Dispose();
            _compositionHost.Dispose();

            this.LoggerFactory.Dispose();
            this.Workspace.Dispose();
        }

        public static OmniSharpTestHost Create(
            IServiceProvider serviceProvider,
            IEnumerable<ExportDescriptorProvider> additionalExports = null,
            [CallerMemberName] string callerName = "")
        {
            var environment = serviceProvider.GetRequiredService<IOmniSharpEnvironment>();
            var compositionHost = new CompositionHostBuilder(serviceProvider, s_lazyAssemblies.Value, additionalExports)
                .Build(environment.TargetDirectory);

            WorkspaceInitializer.Initialize(serviceProvider, compositionHost);

            var host = new OmniSharpTestHost(serviceProvider, compositionHost, callerName);

            // Force workspace to be updated
            var service = host.GetWorkspaceInformationService();
            service.Handle(new WorkspaceInformationRequest()).Wait();

            return host;
        }

        public static OmniSharpTestHost Create(
            string path = null,
            ITestOutputHelper testOutput = null,
            IConfiguration configurationData = null,
            DotNetCliVersion dotNetCliVersion = DotNetCliVersion.Current,
            IEnumerable<ExportDescriptorProvider> additionalExports = null,
            [CallerMemberName] string callerName = "",
            IEventEmitter eventEmitter = null)
        {
            var environment = new OmniSharpEnvironment(path, logLevel: LogLevel.Trace);

            var serviceProvider = TestServiceProvider.Create(testOutput, environment, configurationData, dotNetCliVersion, eventEmitter);

            return Create(serviceProvider, additionalExports, callerName);
        }

        public T GetExport<T>()
            => this._compositionHost.GetExport<T>();

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

        public IEnumerable<ProjectId> AddFilesToWorkspace(params TestFile[] testFiles)
            => AddFilesToWorkspace(Directory.GetCurrentDirectory(), testFiles);

        public IEnumerable<ProjectId> AddFilesToWorkspace(string folderPath, params TestFile[] testFiles)
        {
            folderPath = folderPath ?? Directory.GetCurrentDirectory();

            var csFiles = testFiles.Where(f => f.FileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToArray();
            var vbFiles = testFiles.Where(f => f.FileName.EndsWith(".vb", StringComparison.OrdinalIgnoreCase)).ToArray();

            var projects = TestHelpers.AddProjectToWorkspace(
                    Workspace,
                    Path.Combine(folderPath, "project.csproj"),
                    new[] { "net472" },
                    csFiles);

            if (vbFiles.Length > 0)
            {
                projects = projects.Concat(
                    TestHelpers.AddProjectToWorkspace(
                    Workspace,
                    Path.Combine(folderPath, "project.vbproj"),
                    new[] { "net472" },
                    vbFiles,
                    languageName: LanguageNames.VisualBasic)
                );
            }

            foreach (var csxFile in testFiles.Where(f => f.FileName.EndsWith(".csx", StringComparison.OrdinalIgnoreCase)))
            {
                TestHelpers.AddCsxProjectToWorkspace(Workspace, csxFile);
            }

            return projects;
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
