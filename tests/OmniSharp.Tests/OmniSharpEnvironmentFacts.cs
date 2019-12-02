using Microsoft.Extensions.Logging;
using OmniSharp.Services;
using System.Linq;
using TestUtility;
using Xunit;

namespace OmniSharp.Tests
{
    public class CommandLineApplicationFacts
    {
        [InlineData("-s", @"\path\to\my-solution\foo", "a=b")]
        [InlineData("--source", @"\path\to\my-solution\foo", "a=b")]
        [InlineData("-s", @"\path\to\my=solution\foo", "a=b")]
        [InlineData("--source", @"\path\to\my=solution\foo", "a=b")]
        [InlineData("a=b", "-s", @"\path\to\my-solution\foo")]
        [InlineData("a=b", "--source", @"\path\to\my-solution\foo")]
        [InlineData("a=b", "-s", @"\path\to\my=solution\foo")]
        [InlineData("a=b", "--source", @"\path\to\my=solution\foo")]
        [InlineData("a=b", null, null)]
        [Theory]
        public void AllowsEqualsSignInSolutionPath(string arg1, string arg2, string arg3)
        {
            var app = new CommandLineApplication();
            app.OnExecute(() => 0);
            app.Execute(new[] { arg1, arg2, arg3 });

            Assert.Single(app.OtherArgs);
            Assert.Equal("a=b", app.OtherArgs.First());
        }
    }

    public class OmniSharpEnvironmentFacts
    {
        [Fact]
        public void OmnisharpEnvironmentSetsSolutionPathCorrectly()
        {
            var environment = new OmniSharpEnvironment(TestAssets.Instance.OmniSharpSolutionPath, 1000, LogLevel.Information, null);
            Assert.Equal(TestAssets.Instance.OmniSharpSolutionPath, environment.SolutionFilePath);
        }
        [Fact]
        public void OmnisharpEnvironmentSetsPathCorrectly()
        {
            var environment = new OmniSharpEnvironment(TestAssets.Instance.OmniSharpSolutionPath, 1000, LogLevel.Information, null);
            Assert.Equal(TestAssets.Instance.RootFolder, environment.TargetDirectory);
        }

        [Fact]
        public void OmnisharpEnvironmentHasNullSolutionFilePathIfDirectorySet()
        {
            var environment = new OmniSharpEnvironment(TestAssets.Instance.RootFolder, 1000, LogLevel.Information, null);

            Assert.Null(environment.SolutionFilePath);
        }
    }
}
