using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp;
using OmniSharp.Host.Loader;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Services;
using TestUtility.Logging;
using Xunit.Abstractions;

namespace TestUtility
{
    public abstract class AbstractTestFixture
    {
        private readonly ITestOutputHelper _testOutput;

        protected readonly ILoggerFactory LoggerFactory;
        protected readonly IAssemblyLoader AssemblyLoader;

        protected AbstractTestFixture(ITestOutputHelper output)
        {
            this._testOutput = output;
            this.LoggerFactory = new LoggerFactory()
                .AddXunit(output);
            this.AssemblyLoader = new AssemblyLoader(this.LoggerFactory);
        }

        protected Assembly GetAssembly<T>()
        {
            return typeof(T).GetTypeInfo().Assembly;
        }

        protected virtual IEnumerable<Assembly> GetHostAssemblies()
        {
            yield return GetAssembly<CodeCheckService>();
        }

        private IEnumerable<Assembly> ComputeHostAssemblies(Assembly[] assemblies)
        {
            return assemblies == null || assemblies.Length == 0
                ? GetHostAssemblies()
                : assemblies;
        }

        protected CompositionHost CreatePlugInHost(params Assembly[] assemblies)
        {
            var environment = new OmniSharpEnvironment();
            return Startup.CreateCompositionHost(
                serviceProvider: new TestServiceProvider(environment, this.LoggerFactory),
                options: new OmniSharpOptions(),
                assemblies: ComputeHostAssemblies(assemblies));
        }

        protected Task<OmniSharpWorkspace> CreateWorkspaceAsync(params TestFile[] testFiles)
        {
            var plugInHost = CreatePlugInHost();
            return CreateWorkspaceAsync(plugInHost, testFiles);
        }

        protected async Task<OmniSharpWorkspace> CreateWorkspaceAsync(CompositionHost plugInHost, params TestFile[] testFiles)
        {
            if (plugInHost == null)
            {
                throw new ArgumentNullException(nameof(plugInHost));
            }

            var workspace = plugInHost.GetExport<OmniSharpWorkspace>();

            // OmniSharp ships only one provider, CSharpWorkspaceOptionsProvider
            var formattingProvider = plugInHost.GetExports<IWorkspaceOptionsProvider>().Single() as CSharpWorkspaceOptionsProvider;

            if (formattingProvider != null)
            {
                workspace.Options = formattingProvider.Process(workspace.Options);
            }

            TestHelpers.AddProjectToWorkspace(
                workspace,
                "project.json",
                new[] { "dnx451", "dnxcore50" },
                testFiles);

            await Task.Delay(50);
            return workspace;
        }

        protected TestOmniSharpHost CreateOmniSharpHost(string path = null, IEnumerable<KeyValuePair<string, string>> configurationData = null)
        {
            return TestOmniSharpHost.Create(path, this._testOutput, configurationData);
        }

        protected TestOmniSharpHost CreateOmniSharpHost(params TestFile[] testFiles)
        {
            var host = TestOmniSharpHost.Create(path: null, testOutput: this._testOutput, configurationData: null);

            host.AddFilesToWorkspace(testFiles);

            return host;
        }
    }
}
