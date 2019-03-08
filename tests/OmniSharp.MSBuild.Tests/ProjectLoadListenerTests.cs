using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.Events;
using OmniSharp.MSBuild.Notification;
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
        private VsTfmAndFileExtHashingAlgorithm _tfmAndFileHashingAlgorithm;
        private VsReferenceHashingAlgorithm _referenceHashingAlgorithm;

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
            var expectedTFM = GetHashedTargetFramework(targetFramework);
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            projectInstance.SetProperty(ProjectLoadListener.TargetFrameworkPropertyName, targetFramework);

            // Act
            var tfm = ProjectLoadListener.GetHashedTargetFrameworks(projectInstance);

            // Assert
            Assert.Equal(expectedTFM, tfm.First().Value);
        }

        [Fact]
        public void GetTargetFramework_NoTFM_ReturnsTargetFrameworkVersion()
        {
            // Arrange
            const string targetFramework = "v4.6.1";
            var expectedTFM = GetHashedTargetFramework(targetFramework);
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            projectInstance.SetProperty(ProjectLoadListener.TargetFrameworkVersionPropertyName, targetFramework);

            // Act
            var tfm = ProjectLoadListener.GetHashedTargetFrameworks(projectInstance);

            // Assert
            Assert.Equal(expectedTFM, tfm.First().Value);
        }

        [Fact]
        public void GetTargetFramework_PrioritizesTargetFrameworkOverVersion()
        {
            // Arrange
            const string targetFramework = "v4.6.1";
            var expectedTFM = GetHashedTargetFramework(targetFramework);
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            projectInstance.SetProperty(ProjectLoadListener.TargetFrameworkPropertyName, targetFramework);
            projectInstance.SetProperty(ProjectLoadListener.TargetFrameworkVersionPropertyName, "Unexpected");

            // Act
            var tfm = ProjectLoadListener.GetHashedTargetFrameworks(projectInstance);

            // Assert
            Assert.Equal(expectedTFM, tfm.First().Value);
        }

        [Fact]
        public void GetTargetFramework_NoTFM_ReturnsEmpty()
        {
            // Arrange
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());

            // Act
            var tfm = ProjectLoadListener.GetHashedTargetFrameworks(projectInstance);

            // Assert
            Assert.Empty(tfm);
        }

        [Fact]
        public async Task The_target_framework_is_emitted()
        {
            // Arrange
            var expectedTFM = GetHashedTargetFramework("netcoreapp1.0");
            var messages = new List<ProjectConfigurationMessage>();
            var emitter = new ProjectLoadTestEventEmitter(messages);

            var listener = new ProjectLoadListener(LoggerFactory, emitter);
            var exports = new ExportDescriptorProvider[]
            {
                MefValueProvider.From<IMSBuildEventSink>(listener)
            };

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("HelloWorld"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, exports))
            {
                Assert.Single(messages);
                Assert.Equal(messages[0].TargetFrameworks.First(), expectedTFM);
            }
        }

        [Fact]
        public async Task The_project_file_path_is_emitted()
        {
            // Arrange
            var messages = new List<ProjectConfigurationMessage>();
            var emitter = new ProjectLoadTestEventEmitter(messages);

            var listener = new ProjectLoadListener(LoggerFactory, emitter);
            var exports = new ExportDescriptorProvider[]
            {
                MefValueProvider.From<IMSBuildEventSink>(listener)
            };

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("HelloWorld"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, exports))
            {
                var expectedPath = GetHashedTargetFramework(Directory.GetFiles(testProject.Directory, "*.csproj").SingleOrDefault());
                Assert.Single(messages);
                Assert.Equal(messages[0].ProjectFilePath, expectedPath);
            }
        }

        [Fact]
        public async Task The_references_are_emitted()
        {
            // Arrange
            var messages = new List<ProjectConfigurationMessage>();
            var emitter = new ProjectLoadTestEventEmitter(messages);

            var listener = new ProjectLoadListener(LoggerFactory, emitter);
            var exports = new ExportDescriptorProvider[]
            {
                MefValueProvider.From<IMSBuildEventSink>(listener)
            };

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithReference"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, exports))
            {
                Assert.Single(messages);
                Assert.NotEmpty(messages[0].References);
                Console.WriteLine(messages[0].References);
                //Assert.NotEmpty(messages[0].References.Where(reference => reference ==_referenceHashingAlgorithm.HashInput("System.Core.dll").Value));
            }
        }


        [Fact]
        public async Task If_there_are_multiple_target_frameworks_they_are_returned()
        {
            // Arrange
            var messages = new List<ProjectConfigurationMessage>();
            var emitter = new ProjectLoadTestEventEmitter(messages);

            var listener = new ProjectLoadListener(LoggerFactory, emitter);
            var exports = new ExportDescriptorProvider[]
            {
                MefValueProvider.From<IMSBuildEventSink>(listener)
            };

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithReference"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, exports))
            {
                Assert.Single(messages);
                var tfm = messages[0].TargetFrameworks;
                Assert.Equal(2, tfm.Count());
            }
        }

        private string GetHashedTargetFramework(string targetFramework)
        {
            return _tfmAndFileHashingAlgorithm.HashInput(targetFramework).Value;
        }
    }
}
