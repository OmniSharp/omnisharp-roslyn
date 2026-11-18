using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using TestUtility;
using Xunit;

namespace OmniSharp.MSBuild.Tests
{
    public class ProjectLoadSdkVersionTests : ProjectLoadListenerTestBase
    {
        public ProjectLoadSdkVersionTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [ConditionalFact(typeof(DotnetRuntimeOnly))]
        public async Task The_correct_sdk_version_is_emitted_NET10()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net100Project");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            Assert.Single(emitter.ReceivedMessages);
            Assert.Equal(GetHashedFileExtension("10.0.404"), emitter.ReceivedMessages[0].SdkVersion);
        }

        [ConditionalFact(typeof(DotnetRuntimeOnly))]
        public async Task The_correct_sdk_version_is_emitted_NET11()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net110Project");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            Assert.Single(emitter.ReceivedMessages);
            Assert.Equal(GetHashedFileExtension("11.0.100"), emitter.ReceivedMessages[0].SdkVersion);
        }
    }
}
