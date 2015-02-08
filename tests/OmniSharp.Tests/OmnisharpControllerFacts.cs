using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace OmniSharp.Tests
{
    public class OmnisharpControllerFacts
    {
        private void CreateSimpleWorkspace(out OmnisharpWorkspace workspace, out OmnisharpController controller, out DocumentInfo document, string filename, string contents)
        {
            workspace = new OmnisharpWorkspace();
            controller = new OmnisharpController(workspace, null);

            var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "ProjectNameVal", "AssemblyNameVal", LanguageNames.CSharp);

            document = DocumentInfo.Create(DocumentId.CreateNewId(projectInfo.Id), filename,
                null, SourceCodeKind.Regular,
                TextLoader.From(TextAndVersion.Create(SourceText.From(contents), VersionStamp.Create())),
                filename);

            workspace.AddProject(projectInfo);
            workspace.AddDocument(document);
        }

        [Fact]
        public async Task ChangeBuffer_InsertRemoveChanges()
        {
            OmnisharpWorkspace workspace;
            OmnisharpController controller;
            DocumentInfo document;
            CreateSimpleWorkspace(out workspace, out controller, out document, "test.cs", "class C {}");

            // insert edit
            await controller.ChangeBuffer(new Models.ChangeBufferRequest()
            {
                StartLine = 1,
                StartColumn = 1,
                EndLine = 1,
                EndColumn = 1,
                NewText = "farboo",
                FileName = "test.cs"
            });
            var sourceText = await workspace.CurrentSolution.GetDocument(document.Id).GetTextAsync();
            Assert.Equal("farbooclass C {}", sourceText.ToString());

            // remove edit
            await controller.ChangeBuffer(new Models.ChangeBufferRequest()
            {
                StartLine = 1,
                StartColumn = 1,
                EndLine = 1,
                EndColumn = 7,
                NewText = "",
                FileName = "test.cs"
            });
            sourceText = await workspace.CurrentSolution.GetDocument(document.Id).GetTextAsync();
            Assert.Equal("class C {}", sourceText.ToString());

            // modification edit
            await controller.ChangeBuffer(new Models.ChangeBufferRequest()
            {
                StartLine = 1,
                StartColumn = 1,
                EndLine = 1,
                EndColumn = 6,
                NewText = "interface",
                FileName = "test.cs"
            });
            sourceText = await workspace.CurrentSolution.GetDocument(document.Id).GetTextAsync();
            Assert.Equal("interface C {}", sourceText.ToString());
        }
    }
}