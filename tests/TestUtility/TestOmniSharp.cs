using System;
using System.Composition.Hosting;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp;
using OmniSharp.DotNet;
using OmniSharp.DotNetTest.Helpers.DotNetTestManager;
using OmniSharp.MSBuild;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Intellisense;
using TestUtility.Fake;

namespace TestUtility
{
    public class TestOmniSharp : IDisposable
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

        private bool _disposed;

        public OmniSharpWorkspace Workspace { get; }
        public CompositionHost CompositionHost { get; }

        private TestOmniSharp(OmniSharpWorkspace workspace, CompositionHost compositionHost)
        {
            this.Workspace = workspace;
            this.CompositionHost = compositionHost;
        }

        public static TestOmniSharp Create(string path, IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            var compositionHost = Startup.CreateCompositionHost(
                serviceProvider: new FakeServiceProvider(path, loggerFactory),
                options: new OmniSharpOptions(),
                assemblies: s_lazyAssemblies.Value);

            var workspace = compositionHost.GetExport<OmniSharpWorkspace>();
            var logger = loggerFactory.CreateLogger<TestOmniSharp>();

            Startup.InitializeWorkspace(workspace, compositionHost, configuration, logger);

            return new TestOmniSharp(workspace, compositionHost);
        }

        ~TestOmniSharp()
        {
            throw new InvalidOperationException($"{nameof(TestOmniSharp)}.{nameof(Dispose)}() not called.");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                throw new InvalidOperationException($"{nameof(TestOmniSharp)} already disposed.");
            }

            this._disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
