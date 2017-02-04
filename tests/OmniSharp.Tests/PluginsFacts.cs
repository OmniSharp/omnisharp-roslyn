using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OmniSharp.Plugins;
using OmniSharp.Plugins.CodeActions;
using OmniSharp.Services;
using TestUtility;
using TestUtility.Fake;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Tests
{
    public class PluginsFacts : AbstractTestFixture
    {
        public PluginsFacts(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void LoadsCodeFixesFromExternalAssembly()
        {
            var container = CreatePlugInHost(CustomServices, typeof(PluginCodeActionProvider).GetTypeInfo().Assembly);
            var providers = container.GetExports<ICodeActionProvider>();

            var codeFixes = providers
                .SelectMany(x => x.CodeFixes);

            Assert.Contains(codeFixes, x => x.GetType().FullName.StartsWith("RefactoringEssentials."));
        }

        [Fact]
        public void LoadsRefactoringsFromExternalAssembly()
        {
            var container = CreatePlugInHost(CustomServices, typeof(PluginCodeActionProvider).GetTypeInfo().Assembly);
            var providers = container.GetExports<ICodeActionProvider>();

            var refactorings = providers
                .SelectMany(x => x.Refactorings);

            Assert.Contains(refactorings, x => x.GetType().FullName.StartsWith("RefactoringEssentials."));
        }

        private void CustomServices(IFakeServiceProvider serviceProvider)
        {
            var plugins = new PluginAssemblies(new[]
            {
                Path.Combine(TestAssets.Instance.TestAssetsFolder, "RefactoringEssentials")
            });

            serviceProvider.SetService(typeof(PluginAssemblies), plugins);
        }
    }
}
