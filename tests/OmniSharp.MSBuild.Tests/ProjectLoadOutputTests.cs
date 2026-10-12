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
    public class ProjectLoadOutputTests : ProjectLoadListenerTestBase
    {
        public ProjectLoadOutputTests(ITestOutputHelper output)
            : base(output)
        {
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
    }
}
