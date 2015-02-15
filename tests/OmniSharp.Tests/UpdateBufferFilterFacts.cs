using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.Filters;
using Xunit;

namespace OmniSharp.Tests
{
    public class UpdateBufferFilterFacts
    {
        [Fact]
        public async Task UpdateBuffer_HandlesVoidRequest()
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>
            {
                { "test.cs", "class C {}" }
            });

            var bufferFilter = new UpdateBufferFilter(workspace);
            var docId = workspace.CurrentSolution.GetDocumentIdsWithFilePath("test.cs").First();

            // ignore void buffers
            bufferFilter.OnActionExecuting(TestHelpers.CreateActionExecutingContext(new Models.Request() { }));
            var sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
            Assert.Equal("class C {}", sourceText.ToString());

            bufferFilter.OnActionExecuting(TestHelpers.CreateActionExecutingContext(new Models.Request() { FileName = "test.cs" }));
            sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
            Assert.Equal("class C {}", sourceText.ToString());

            bufferFilter.OnActionExecuting(TestHelpers.CreateActionExecutingContext(new Models.Request() { Buffer = "// c", FileName = "some_other_file.cs" }));
            sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
            Assert.Equal("class C {}", sourceText.ToString());

            // valid updates
            bufferFilter.OnActionExecuting(TestHelpers.CreateActionExecutingContext(new Models.Request() { FileName = "test.cs", Buffer = "interface I {}" }));
            sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
            Assert.Equal("interface I {}", sourceText.ToString());

            bufferFilter.OnActionExecuting(TestHelpers.CreateActionExecutingContext(new Models.Request() { FileName = "test.cs", Buffer = "" }));
            sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
            Assert.Equal("", sourceText.ToString());
        }
    }
}