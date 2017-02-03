using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using TestUtility.Fake;
using TestUtility.Logging;
using Xunit.Abstractions;

namespace TestUtility
{
    public abstract class AbstractTestFixture
    {
        private readonly ITestOutputHelper _output;

        protected readonly ILoggerFactory LoggerFactory;

        protected AbstractTestFixture(ITestOutputHelper output)
        {
            this._output = output;
            this.LoggerFactory = new LoggerFactory()
                .AddXunit(output);
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
            return Startup.ConfigureMef(
                serviceProvider: new FakeServiceProvider(this.LoggerFactory),
                options: new FakeOmniSharpOptions().Value,
                assemblies: ComputeHostAssemblies(assemblies));
        }

        protected Task<OmnisharpWorkspace> CreateWorkspaceAsync(params TestFile[] testFiles)
        {
            var plugInHost = CreatePlugInHost();
            return CreateWorkspaceAsync(plugInHost, testFiles);
        }

        protected async Task<OmnisharpWorkspace> CreateWorkspaceAsync(CompositionHost plugInHost, params TestFile[] testFiles)
        {
            if (plugInHost == null)
            {
                throw new ArgumentNullException(nameof(plugInHost));
            }

            var workspace = plugInHost.GetExport<OmnisharpWorkspace>();

            await TestHelpers.AddProjectToWorkspaceAsync(
                workspace,
                "project.json",
                new[] { "dnx451", "dnxcore50" },
                testFiles);

            await Task.Delay(50);
            return workspace;
        }
    }
}
