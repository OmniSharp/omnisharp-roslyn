using Microsoft.Extensions.Logging;
using OmniSharp.Services;
using TestUtility;
using Xunit;

namespace OmniSharp.Tests
{
    public class OmniSharpEnvironmentFacts
    {
        [Fact]
        public void OmnisharpEnvironmentSetsSolutionPathCorrectly()
        {
            var environment = new OmniSharpEnvironment(TestAssets.Instance.OmniSharpSolutionPath, 1000, -1, LogLevel.Information, TransportType.Http, null);
            Assert.Equal(TestAssets.Instance.OmniSharpSolutionPath, environment.SolutionFilePath);
        }
        [Fact]
        public void OmnisharpEnvironmentSetsPathCorrectly()
        {
            var environment = new OmniSharpEnvironment(TestAssets.Instance.OmniSharpSolutionPath, 1000, -1, LogLevel.Information, TransportType.Http, null);
            Assert.Equal(TestAssets.Instance.RootFolder, environment.TargetDirectory);
        }

        [Fact]
        public void OmnisharpEnvironmentSetsPortCorrectly()
        {
            var environment = new OmniSharpEnvironment(TestAssets.Instance.OmniSharpSolutionPath, 1000, -1, LogLevel.Information, TransportType.Http, null);
            Assert.Equal(1000, environment.Port);
        }

        [Fact]
        public void OmnisharpEnvironmentHasNullSolutionFilePathIfDirectorySet()
        {
            var environment = new OmniSharpEnvironment(TestAssets.Instance.RootFolder, 1000, -1, LogLevel.Information, TransportType.Http, null);

            Assert.Null(environment.SolutionFilePath);
        }
    }
}
