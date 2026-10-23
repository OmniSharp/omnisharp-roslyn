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
    public class ProjectLoadIdentityTests : ProjectLoadListenerTestBase
    {
        public ProjectLoadIdentityTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task The_target_framework_is_emitted()
        {
            // Arrange
            var expectedTFM = "net8.0";
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("HelloWorld");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            Assert.Single(emitter.ReceivedMessages);
            Assert.Equal(emitter.ReceivedMessages[0].TargetFrameworks.First(), expectedTFM);
        }

        [Fact]
        public async Task If_there_is_a_solution_file_the_project_guid_present_in_it_is_emitted()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolution");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            var expectedGuid = "A4C2694D-AEB4-4CB1-8951-5290424EF883".ToLower();
            Assert.Single(emitter.ReceivedMessages);
            Assert.Equal(emitter.ReceivedMessages[0].ProjectId, expectedGuid);
        }

        [Fact]
        public async Task If_there_is_no_solution_file_the_hash_of_project_file_content_and_name_is_emitted()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("HelloWorld");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            var projectFileContent = File.ReadAllText(Directory.GetFiles(testProject.Directory, "*.csproj").Single());
            var expectedGuid = GetHashedReference($"Filename: HelloWorld.csproj\n{projectFileContent}");
            Assert.Single(emitter.ReceivedMessages);
            Assert.Equal(emitter.ReceivedMessages[0].ProjectId, expectedGuid);
        }

        [Fact]
        public async Task Given_a_project_with_a_package_reference_the_references_are_emitted()
        {
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithWildcardPackageReference");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            Assert.Single(emitter.ReceivedMessages);
            Assert.Contains(emitter.ReceivedMessages[0].References, reference => reference == GetHashedReference("newtonsoft.json"));
        }

        [Fact]
        public async Task If_there_are_multiple_target_frameworks_they_are_emitted()
        {
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMultiTFMLib/Lib");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            Assert.Single(emitter.ReceivedMessages);
            var tfm = emitter.ReceivedMessages[0].TargetFrameworks.ToArray();
            Assert.Equal(2, tfm.Count());
            Assert.Equal("netstandard1.5", tfm[0]);
            Assert.Equal("netstandard2.0", tfm[1]);
        }

        [Fact]
        public async Task The_hashed_references_of_the_source_files_are_emitted()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("HelloWorld");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            Assert.Single(emitter.ReceivedMessages);
            Assert.Single(emitter.ReceivedMessages[0].FileExtensions);
            Assert.Equal(emitter.ReceivedMessages[0].FileExtensions.First(), GetHashedFileExtension(".cs"));
        }
    }
}
