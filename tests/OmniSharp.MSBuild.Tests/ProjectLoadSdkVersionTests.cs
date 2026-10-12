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
        public async Task The_correct_sdk_version_is_emitted_NET8()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net80Project");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            Assert.Single(emitter.ReceivedMessages);
            Assert.Equal(GetHashedFileExtension("8.0.425"), emitter.ReceivedMessages[0].SdkVersion);
        }

        [ConditionalFact(typeof(DotnetRuntimeOnly))]
        public async Task The_correct_sdk_version_is_emitted_NET9()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net90Project");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            Assert.Single(emitter.ReceivedMessages);
            Assert.Equal(GetHashedFileExtension("9.0.318"), emitter.ReceivedMessages[0].SdkVersion);
        }

        [ConditionalFact(typeof(DotnetRuntimeOnly))]
        public async Task The_correct_sdk_version_is_emitted_NET10()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net100Project");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            Assert.Single(emitter.ReceivedMessages);
            Assert.Equal(GetHashedFileExtension("10.0.401"), emitter.ReceivedMessages[0].SdkVersion);
        }

        [ConditionalFact(typeof(DotnetRuntimeOnly))]
        public async Task The_correct_sdk_version_is_emitted_NET11()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net110Project");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            Assert.Single(emitter.ReceivedMessages);
            Assert.Equal(GetHashedFileExtension("11.0.100-rc.1.26425.128"), emitter.ReceivedMessages[0].SdkVersion);
        }
    }
}
