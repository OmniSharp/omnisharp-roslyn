using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models.ChangeBuffer;
using OmniSharp.Roslyn.CSharp.Services.Buffer;
using OmniSharp.Services;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class BufferFacts
    {
        private (OmniSharpWorkspace, ChangeBufferService, DocumentInfo) CreateSimpleWorkspace(string fileName, string contents)
        {
            var workspace = new OmniSharpWorkspace(
                new HostServicesAggregator(
                    Enumerable.Empty<IHostServicesProvider>()));

            var service = new ChangeBufferService(workspace);

            var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "ProjectNameVal", "AssemblyNameVal", LanguageNames.CSharp);

            var documentInfo = DocumentInfo.Create(DocumentId.CreateNewId(projectInfo.Id), fileName,
                null, SourceCodeKind.Regular,
                TextLoader.From(TextAndVersion.Create(SourceText.From(contents), VersionStamp.Create())),
                fileName);

            workspace.AddProject(projectInfo);
            workspace.AddDocument(documentInfo);

            return (workspace, service, documentInfo);
        }

        [Fact]
        public async Task ChangeBuffer_InsertRemoveChanges()
        {
            var (workspace, controller, documentInfo) = CreateSimpleWorkspace("test.cs", "class C {}");

            // insert edit
            await controller.Handle(new ChangeBufferRequest()
            {
                StartLine = 0,
                StartColumn = 0,
                EndLine = 0,
                EndColumn = 0,
                NewText = "farboo",
                FileName = "test.cs"
            });

            var sourceText = await workspace.CurrentSolution.GetDocument(documentInfo.Id).GetTextAsync();
            Assert.Equal("farbooclass C {}", sourceText.ToString());

            // remove edit
            await controller.Handle(new ChangeBufferRequest()
            {
                StartLine = 0,
                StartColumn = 0,
                EndLine = 0,
                EndColumn = 6,
                NewText = "",
                FileName = "test.cs"
            });

            sourceText = await workspace.CurrentSolution.GetDocument(documentInfo.Id).GetTextAsync();
            Assert.Equal("class C {}", sourceText.ToString());

            // modification edit
            await controller.Handle(new ChangeBufferRequest()
            {
                StartLine = 0,
                StartColumn = 0,
                EndLine = 0,
                EndColumn = 5,
                NewText = "interface",
                FileName = "test.cs"
            });

            sourceText = await workspace.CurrentSolution.GetDocument(documentInfo.Id).GetTextAsync();
            Assert.Equal("interface C {}", sourceText.ToString());
        }
    }
}
