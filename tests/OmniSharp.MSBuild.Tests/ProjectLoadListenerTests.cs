using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;
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
        public async Task Given_a_restored_project_the_references_are_emitted()
        {
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net80Project");
            await RestoreProject(testProject);
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            Assert.Single(emitter.ReceivedMessages);
            Assert.Contains(emitter.ReceivedMessages[0].References, reference => reference == GetHashedReference("system.runtime"));
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

        [Fact]
        public async Task The_output_kind_is_emitted()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net80Project");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            Assert.Single(emitter.ReceivedMessages);
            Assert.Equal((int)OutputKind.ConsoleApplication, emitter.ReceivedMessages[0].OutputKind);
        }

        [Fact]
        public async Task The_correct_project_capablities_is_emitted()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();
            var expectedCapabilities = new HashSet<string>()
                {
                    "GenerateDocumentationFile",
                    "CSharp",
                    "Managed",
                    "RelativePathDerivedDefaultNamespace",
                    "AssemblyReferences",
                    "COMReferences",
                    "ProjectReferences",
                    "SharedProjectReferences",
                    "OutputGroups",
                    "AllTargetOutputGroups",
                    "VisualStudioWellKnownOutputGroups",
                    "SingleFileGenerators",
                    "DeclaredSourceItems",
                    "UserSourceItems",
                    "CrossPlatformExecutable",
                    "Pack",
                };

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net80Project");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            Assert.Single(emitter.ReceivedMessages);

            Assert.ProperSuperset(expectedCapabilities, emitter.ReceivedMessages[0].ProjectCapabilities.ToHashSet());
        }

        [ConditionalFact(typeof(DotnetRuntimeOnly))]
        public async Task The_correct_sdk_version_is_emitted_NET8()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net80Project");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            var sdkVersion = host.GetExport<IDotNetCliService>().GetVersion(testProject.Directory).Version.ToString();
            Assert.Single(emitter.ReceivedMessages);
            Assert.Equal(GetHashedFileExtension(sdkVersion), emitter.ReceivedMessages[0].SdkVersion);
        }

        [ConditionalFact(typeof(DotnetRuntimeOnly))]
        public async Task The_correct_sdk_version_is_emitted_NET9()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net90Project");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            var sdkVersion = host.GetExport<IDotNetCliService>().GetVersion(testProject.Directory).Version.ToString();
            Assert.Single(emitter.ReceivedMessages);
            Assert.Equal(GetHashedFileExtension(sdkVersion), emitter.ReceivedMessages[0].SdkVersion);
        }

        [ConditionalFact(typeof(DotnetRuntimeOnly))]
        public async Task The_correct_sdk_version_is_emitted_NET10()
        {
            // Arrange
            var emitter = new ProjectLoadTestEventEmitter();

            using var testProject = await TestAssets.Instance.GetTestProjectAsync("Net100Project");
            using var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory));
            var sdkVersion = host.GetExport<IDotNetCliService>().GetVersion(testProject.Directory).Version.ToString();
            Assert.Single(emitter.ReceivedMessages);
            Assert.Equal(GetHashedFileExtension(sdkVersion), emitter.ReceivedMessages[0].SdkVersion);
        }

        private string GetHashedFileExtension(string fileExtension)
        {
            return _tfmAndFileHashingAlgorithm.HashInput(fileExtension).Value;
        }
        private string GetHashedReference(string reference)
        {
            return _referenceHashingAlgorithm.HashInput(reference).Value;
        }

        private static async Task RestoreProject(ITestProject testProject)
        {
            var options = new OmniSharp.Options.DotNetCliOptions
            {
                LocationPaths = new[]
                {
                    Path.Combine(TestAssets.Instance.RootFolder, DotNetCliVersion.Current.GetFolderName())
                }
            };

            await new DotNetCliService(
                new LoggerFactory(),
                OmniSharp.Eventing.NullEventEmitter.Instance,
                Microsoft.Extensions.Options.Options.Create(options),
                new OmniSharpEnvironment(testProject.Directory)).RestoreAsync(testProject.Directory);
        }
    }
}
