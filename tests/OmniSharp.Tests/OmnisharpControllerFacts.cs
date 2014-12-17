using Microsoft.CodeAnalysis;
using Xunit;
using Microsoft.CodeAnalysis.Text;

namespace OmniSharp.Tests
{
    public class OmnisharpControllerFacts
    {

        [Fact]
        public async void ChangeBuffer_InsertRemoveChanges()
        {
            var workspace = new OmnisharpWorkspace();
            var controller = new OmnisharpController(workspace);

            var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "ProjectNameVal", "AssemblyNameVal", LanguageNames.CSharp);

            var document = DocumentInfo.Create(DocumentId.CreateNewId(projectInfo.Id), "test.cs",
                null, SourceCodeKind.Regular,
                TextLoader.From(TextAndVersion.Create(SourceText.From("class C {}"), VersionStamp.Create())),
                "test.cs");

            workspace.AddProject(projectInfo);
            workspace.AddDocument(document);

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