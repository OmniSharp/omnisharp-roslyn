using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.FileWatching;
using OmniSharp.Services;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class OmniSharpWorkspaceFacts
    {
        public OmniSharpWorkspaceFacts()
        {
            Workspace = new OmniSharpWorkspace(
                new HostServicesAggregator(
                    Enumerable.Empty<IHostServicesProvider>(),
                    new LoggerFactory()),
                new LoggerFactory(), new ManualFileSystemWatcher());
        }

        public OmniSharpWorkspace Workspace { get; }

        [Fact]
        public void TryPromoteMiscellaneousDocumentsToProject_RelatedProject_PromotesAllDocuments()
        {
            // Arrange
            var project = AddProjectToWorkspace("/path/to/project.csproj");
            Workspace.TryAddMiscellaneousDocument("/path/to/file1.cs", LanguageNames.CSharp);
            Workspace.TryAddMiscellaneousDocument("/path/to/file2.cs", LanguageNames.CSharp);

            // Act
            Workspace.TryPromoteMiscellaneousDocumentsToProject(project);

            // Assert
            project = Workspace.CurrentSolution.GetProject(project.Id);
            Assert.Collection(
                project.Documents,
                document => Assert.Equal("/path/to/file1.cs", document.FilePath),
                document => Assert.Equal("/path/to/file2.cs", document.FilePath));

            var miscProject = Assert.Single(Workspace.CurrentSolution.Projects.Except(new[] { project }));
            Assert.Empty(miscProject.DocumentIds);
        }

        [Fact]
        public void TryPromoteMiscellaneousDocumentsToProject_UnrelatedProject_Noops()
        {
            // Arrange
            var project = AddProjectToWorkspace("/path/to/project.csproj");
            var expectedDocumentId = Workspace.TryAddMiscellaneousDocument("/misc/file.cs", LanguageNames.CSharp);

            // Act
            Workspace.TryPromoteMiscellaneousDocumentsToProject(project);

            // Assert
            project = Workspace.CurrentSolution.GetProject(project.Id);
            Assert.Empty(project.DocumentIds);
            var miscProject = Assert.Single(Workspace.CurrentSolution.Projects.Except(new[] { project }));
            var document = Assert.Single(miscProject.DocumentIds);
            Assert.Equal(expectedDocumentId.Id, document.Id);
        }

        [Fact]
        public void FileBelongsToProject_EmptyProjectFilePath_ReturnsFalse()
        {
            // Arrange
            var project = AddProjectToWorkspace(filePath: string.Empty);

            // Act
            var result = Workspace.FileBelongsToProject("/path/to/file.cs", project);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void FileBelongsToProject_EmptyFilePath_ReturnsFalse()
        {
            // Arrange
            var project = AddProjectToWorkspace(filePath: "/path/to/project.csproj");

            // Act
            var result = Workspace.FileBelongsToProject(string.Empty, project);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void FileBelongsToProject_UnrelatedFile_ReturnsFalse()
        {
            // Arrange
            var project = AddProjectToWorkspace(filePath: "/path/to/project.csproj");
            var filePath = "/different/path/file.cs";

            // Act
            var result = Workspace.FileBelongsToProject(filePath, project);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void FileBelongsToProject_Sibling_ReturnsTrue()
        {
            // Arrange
            var project = AddProjectToWorkspace(filePath: "/path/to/project.csproj");
            var filePath = "/path/to/file.cs";

            // Act
            var result = Workspace.FileBelongsToProject(filePath, project);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void FileBelongsToProject_Descendant_ReturnsTrue()
        {
            // Arrange
            var project = AddProjectToWorkspace(filePath: "/path/to/project.csproj");
            var filePath = "/path/to/folder/file.cs";

            // Act
            var result = Workspace.FileBelongsToProject(filePath, project);

            // Assert
            Assert.True(result);
        }

        private Project AddProjectToWorkspace(string filePath)
        {
            var projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "ProjectNameVal",
                "AssemblyNameVal",
                LanguageNames.CSharp,
                filePath);
            Workspace.AddProject(projectInfo);
            var project = Workspace.CurrentSolution.GetProject(projectInfo.Id);
            return project;
        }
    }
}
