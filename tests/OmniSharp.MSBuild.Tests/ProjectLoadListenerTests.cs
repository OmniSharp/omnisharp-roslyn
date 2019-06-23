using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.Events;
using OmniSharp.MSBuild.Notification;
using OmniSharp.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition.Hosting.Core;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public partial class ProjectLoadListenerTests : AbstractMSBuildTestFixture
    {
        private readonly VsTfmAndFileExtHashingAlgorithm _tfmAndFileHashingAlgorithm;
        private readonly VsReferenceHashingAlgorithm _referenceHashingAlgorithm;

        public ProjectLoadListenerTests(ITestOutputHelper output) : base(output)
        {
            _tfmAndFileHashingAlgorithm = new VsTfmAndFileExtHashingAlgorithm();
            _referenceHashingAlgorithm = new VsReferenceHashingAlgorithm();
        }

        [Fact]
        public void GetTargetFramework_ReturnsTargetFramework()
        {
            // Arrange

            const string targetFramework = "net461";
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            projectInstance.SetProperty(ProjectLoadListener.TargetFramework, targetFramework);

            // Act
            var tfm = ProjectLoadListener.GetTargetFrameworks(projectInstance);

            // Assert
            Assert.Equal(targetFramework, tfm.First());
        }

        [Fact]
        public void GetTargetFramework_NoTFM_ReturnsTargetFrameworkVersion()
        {
            // Arrange
            const string targetFramework = "v4.6.1";
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            projectInstance.SetProperty(ProjectLoadListener.TargetFrameworkVersion, targetFramework);

            // Act
            var tfm = ProjectLoadListener.GetTargetFrameworks(projectInstance);

            // Assert
            Assert.Equal(targetFramework, tfm.First());
        }

        [Fact]
        public void GetTargetFramework_PrioritizesTargetFrameworkOverVersion()
        {
            // Arrange
            const string targetFramework = "v4.6.1";
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            projectInstance.SetProperty(ProjectLoadListener.TargetFramework, targetFramework);
            projectInstance.SetProperty(ProjectLoadListener.TargetFrameworkVersion, "Unexpected");

            // Act
            var tfm = ProjectLoadListener.GetTargetFrameworks(projectInstance);

            // Assert
            Assert.Equal(targetFramework, tfm.First());
        }

        [Fact]
        public void GetTargetFramework_NoTFM_ReturnsEmpty()
        {
            // Arrange
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());

            // Act
            var tfm = ProjectLoadListener.GetTargetFrameworks(projectInstance);

            // Assert
            Assert.Empty(tfm);
        }

        [Fact]
        public async Task The_target_framework_is_emitted()
        {
            // Arrange
            var expectedTFM = "netcoreapp2.1";
            var emitter = new ProjectLoadTestEventEmitter();

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("HelloWorld"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory)))
            {
                Assert.Single(emitter.ReceivedMessages);
                Assert.Equal(emitter.ReceivedMessages[0].TargetFrameworks.First(), expectedTFM);
            }
        }

        [Fact]
        public async Task If_there_is_a_solution_file_the_project_guid_present_in_it_is_emitted()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectAndSolution"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory)))
            {
                var expectedGuid = "A4C2694D-AEB4-4CB1-8951-5290424EF883".ToLower();
                Assert.Single(emitter.ReceivedMessages);
                Assert.Equal(emitter.ReceivedMessages[0].ProjectId, expectedGuid);
            }
        }

        [Fact]
        public async Task If_there_is_no_solution_file_the_hash_of_project_file_content_and_name_is_emitted()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("HelloWorld"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory)))
            {
                var projectFileContent = File.ReadAllText(Directory.GetFiles(testProject.Directory, "*.csproj").Single());
                var expectedGuid = GetHashedReference($"Filename: HelloWorld.csproj\n{projectFileContent}");
                Assert.Single(emitter.ReceivedMessages);
                Assert.Equal(emitter.ReceivedMessages[0].ProjectId, expectedGuid);
            }
        }

        [Fact]
        public async Task Given_a_restored_project_the_references_are_emitted()
        {
            var emitter = new ProjectLoadTestEventEmitter();

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("HelloWorld"))
            {
                var dotnetCliService = new DotNetCliService(LoggerFactory, emitter);
                await dotnetCliService.RestoreAsync(testProject.Directory);
                using (var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory)))
                {
                    Assert.Single(emitter.ReceivedMessages);
                    Assert.NotEmpty(emitter.ReceivedMessages[0].References.Where(reference => reference == GetHashedReference("system.core")));
                }
            }
        }


        [Fact]
        public async Task If_there_are_multiple_target_frameworks_they_are_emitted()
        {
            var emitter = new ProjectLoadTestEventEmitter();

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMultiTFMLib/Lib"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory)))
            {
                Assert.Single(emitter.ReceivedMessages);
                var tfm = emitter.ReceivedMessages[0].TargetFrameworks.ToArray();
                Assert.Equal(2, tfm.Count());
                Assert.Equal("netstandard1.3", tfm[0]);
                Assert.Equal("netstandard2.0",tfm[1]);
            }
        }

        [Fact]
        public async Task The_hashed_references_of_the_source_files_are_emitted()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("HelloWorld"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory)))
            {
                Assert.Single(emitter.ReceivedMessages);
                Assert.Single(emitter.ReceivedMessages[0].FileExtensions);
                Assert.Equal(emitter.ReceivedMessages[0].FileExtensions.First(), GetHashedFileExtension(".cs"));
            }
        }

        private string GetHashedFileExtension(string fileExtension)
        {
            return _tfmAndFileHashingAlgorithm.HashInput(fileExtension).Value;
        }
        private string GetHashedReference(string reference)
        {
            return _referenceHashingAlgorithm.HashInput(reference).Value;
        }
    }
}
