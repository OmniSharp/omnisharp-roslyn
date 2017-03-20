using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp;
using OmniSharp.DotNet;
using OmniSharp.DotNetTest.Helpers.DotNetTestManager;
using OmniSharp.Mef;
using OmniSharp.MSBuild;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Intellisense;
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
            typeof(OmnisharpEndpoints).GetTypeInfo().Assembly, // OmniSharp.Abstractions
            typeof(Startup).GetTypeInfo().Assembly, // OmniSharp.Host
            typeof(DotNetProjectSystem).GetTypeInfo().Assembly, // OmniSharp.DotNet
            typeof(DotNetTestManager).GetTypeInfo().Assembly, // OmniSharp.DotNetTest
            typeof(MSBuildProjectSystem).GetTypeInfo().Assembly, // OmniSharp.MSBuild
            typeof(OmniSharpWorkspace).GetTypeInfo().Assembly, // OmniSharp.Roslyn
            typeof(IntellisenseService).GetTypeInfo().Assembly // OmniSharp.Roslyn.CSharp
        });

        private readonly TestServiceProvider _serviceProvider;
        private readonly CompositionHost _compositionHost;

        private Dictionary<(string name, string language), Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> _handlers;

        public ILoggerFactory LoggerFactory { get; }
        public OmniSharpWorkspace Workspace { get; }

        private OmniSharpTestHost(
            TestServiceProvider serviceProvider,
            ILoggerFactory loggerFactory,
            OmniSharpWorkspace workspace,
            CompositionHost compositionHost)
        {
            this._serviceProvider = serviceProvider;
            this._compositionHost = compositionHost;

            this.LoggerFactory = loggerFactory;
            this.Workspace = workspace;
        }

        ~OmniSharpTestHost()
        {
            throw new InvalidOperationException($"{nameof(OmniSharpTestHost)}.{nameof(Dispose)}() not called.");
        }

        protected override void DisposeCore(bool disposing)
        {
            this._serviceProvider.Dispose();
            this._compositionHost.Dispose();

            this.LoggerFactory.Dispose();
            this.Workspace.Dispose();
        }

        public static OmniSharpTestHost Create(string path = null, ITestOutputHelper testOutput = null, IEnumerable<KeyValuePair<string, string>> configurationData = null)
        {
            var builder = new ConfigurationBuilder();
            builder.AddInMemoryCollection(configurationData);
            var configuration = builder.Build();

            var environment = new OmniSharpEnvironment(path);
            var loggerFactory = new LoggerFactory().AddXunit(testOutput);
            var sharedTextWriter = new TestSharedTextWriter(testOutput);
            var serviceProvider = new TestServiceProvider(environment, loggerFactory, sharedTextWriter);

            var compositionHost = Startup.CreateCompositionHost(
                serviceProvider,
                options: new OmniSharpOptions(),
                assemblies: s_lazyAssemblies.Value);

            var workspace = compositionHost.GetExport<OmniSharpWorkspace>();
            var logger = loggerFactory.CreateLogger<OmniSharpTestHost>();

            Startup.InitializeWorkspace(workspace, compositionHost, configuration, logger);

            return new OmniSharpTestHost(serviceProvider, loggerFactory, workspace, compositionHost);
        }

        public T GetExport<T>()
        {
            return this._compositionHost.GetExport<T>();
        }

        public THandler GetRequestHandler<THandler>(string name) where THandler : IRequestHandler
        {
            if (_handlers == null)
            {
                var exports = this._compositionHost.GetExports<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>>();
                _handlers = exports.ToDictionary(
                    keySelector: export => (export.Metadata.EndpointName, export.Metadata.Language),
                    elementSelector: export => export);
            }

            return (THandler)_handlers[(name, LanguageNames.CSharp)].Value;
        }

        public void AddFilesToWorkspace(params TestFile[] testFiles)
        {
            TestHelpers.AddProjectToWorkspace(
                this.Workspace,
                "project.json",
                new[] { "dnx451", "dnxcore50" },
                testFiles);
        }
    }
}
