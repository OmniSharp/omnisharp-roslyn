using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Tests
{
    public class UpdateBufferFilterFacts : AbstractTestFixture
    {
        public UpdateBufferFilterFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task UpdateBuffer_HandlesVoidRequest()
        {
            var testFile = new TestFile("test.cs", "class C {}");
            var workspace = await CreateWorkspaceAsync(testFile);

            var docId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(testFile.FileName).First();

            // ignore void buffers
            await workspace.BufferManager.UpdateBuffer(new Request() { });
            var sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
            Assert.Equal("class C {}", sourceText.ToString());

            await workspace.BufferManager.UpdateBuffer(new Request() { FileName = testFile.FileName });
            sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
            Assert.Equal("class C {}", sourceText.ToString());

            await workspace.BufferManager.UpdateBuffer(new Request() { Buffer = "// c", FileName = "some_other_file.cs" });
            sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
            Assert.Equal("class C {}", sourceText.ToString());

            // valid updates
            await workspace.BufferManager.UpdateBuffer(new Request() { FileName = testFile.FileName, Buffer = "interface I {}" });
            sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
            Assert.Equal("interface I {}", sourceText.ToString());

            await workspace.BufferManager.UpdateBuffer(new Request() { FileName = testFile.FileName, Buffer = "" });
            sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
            Assert.Equal("", sourceText.ToString());
        }

        [Fact]
        public async Task UpdateBuffer_AddsNewDocumentsIfNeeded()
        {
            var testFile = new TestFile("test.cs", "class C {}");
            var workspace = await CreateWorkspaceAsync(testFile);

            await workspace.BufferManager.UpdateBuffer(new Request() { FileName = "test2.cs", Buffer = "interface I {}" });

            Assert.Equal(2, workspace.CurrentSolution.GetDocumentIdsWithFilePath("test2.cs").Length);
            var docId = workspace.CurrentSolution.GetDocumentIdsWithFilePath("test2.cs").FirstOrDefault();
            Assert.NotNull(docId);
            var sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
            Assert.Equal("interface I {}", sourceText.ToString());

            docId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(testFile.FileName).FirstOrDefault();
            Assert.NotNull(docId);
            sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
            Assert.Equal("class C {}", sourceText.ToString());
        }

        [Fact]
        public async Task UpdateBuffer_TransientDocumentsDisappearWhenProjectAddsThem()
        {
            var testFile = new TestFile("test.cs", "class C {}");
            var workspace = await CreateWorkspaceAsync(testFile);

            await workspace.BufferManager.UpdateBuffer(new Request() { FileName = "transient.cs", Buffer = "interface I {}" });

            var docIds = workspace.CurrentSolution.GetDocumentIdsWithFilePath("transient.cs");
            Assert.Equal(2, docIds.Length);

            // simulate a project system adding the file for real
            var project1 = workspace.CurrentSolution.Projects.First();
            var document = DocumentInfo.Create(DocumentId.CreateNewId(project1.Id), "transient.cs",
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From("enum E{}"), VersionStamp.Create())),
                filePath: "transient.cs");
            workspace.CurrentSolution.AddDocument(document);

            docIds = workspace.CurrentSolution.GetDocumentIdsWithFilePath("transient.cs");
            Assert.Equal(2, docIds.Length);

            await workspace.BufferManager.UpdateBuffer(new Models.Request() { FileName = "transient.cs", Buffer = "enum E {}" });
            var sourceText = await workspace.CurrentSolution.GetDocument(docIds.First()).GetTextAsync();
            Assert.Equal("enum E {}", sourceText.ToString());
            sourceText = await workspace.CurrentSolution.GetDocument(docIds.Last()).GetTextAsync();
            Assert.Equal("enum E {}", sourceText.ToString());
        }
    }
}
