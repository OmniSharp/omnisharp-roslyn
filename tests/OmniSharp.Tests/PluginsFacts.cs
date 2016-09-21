using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Plugins;
using OmniSharp.Services;
using TestCommon;
using TestUtility.Fake;
using Xunit;

namespace OmniSharp.Tests
{
    public class PluginsFacts
    {
        [Fact]
        public void LoadsCodeFixesFromExternalAssembly()
        {
            var plugins = new PluginAssemblies(new[]
            {
                Path.Combine(TestsContext.Default.TestRoot, "RefactoringEssentials")
            });
            var container = Startup.ConfigureMef(
                new TestServiceProvider(new FakeLoggerFactory()),
                new FakeOmniSharpOptions().Value,
                plugins.Assemblies);

            var providers = container.GetExports<ICodeActionProvider>();

            var codeFixes = providers
                .SelectMany(x => x.CodeFixes);

            Assert.Contains(codeFixes, x => x.GetType().FullName.StartsWith("RefactoringEssentials."));
        }

        [Fact]
        public void LoadsRefactoringsFromExternalAssembly()
        {
            var plugins = new PluginAssemblies(new[]
            {
                Path.Combine(TestsContext.Default.TestRoot, "RefactoringEssentials")
            });
            var container = Startup.ConfigureMef(
                new TestServiceProvider(new FakeLoggerFactory()),
                new FakeOmniSharpOptions().Value,
                plugins.Assemblies);

            var providers = container.GetExports<ICodeActionProvider>();

            var refactorings = providers
                .SelectMany(x => x.Refactorings);

            Assert.Contains(refactorings, x => x.GetType().FullName.StartsWith("RefactoringEssentials."));
        }
    }
}
