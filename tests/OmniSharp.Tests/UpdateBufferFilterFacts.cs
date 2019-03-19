using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using System;
using System.Threading;

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
            using (var host = CreateOmniSharpHost(testFile))
            {
                var docId = host.Workspace.CurrentSolution.GetDocumentIdsWithFilePath(testFile.FileName).First();

                // ignore void buffers
                await host.Workspace.BufferManager.UpdateBufferAsync(new Request() { });
                var sourceText = await host.Workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
                Assert.Equal("class C {}", sourceText.ToString());

                await host.Workspace.BufferManager.UpdateBufferAsync(new Request() { FileName = testFile.FileName });
                sourceText = await host.Workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
                Assert.Equal("class C {}", sourceText.ToString());

                await host.Workspace.BufferManager.UpdateBufferAsync(new Request() { Buffer = "// c", FileName = "some_other_file.cs" });
                sourceText = await host.Workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
                Assert.Equal("class C {}", sourceText.ToString());

                // valid updates
                await host.Workspace.BufferManager.UpdateBufferAsync(new Request() { FileName = testFile.FileName, Buffer = "interface I {}" });
                sourceText = await host.Workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
                Assert.Equal("interface I {}", sourceText.ToString());

                await host.Workspace.BufferManager.UpdateBufferAsync(new Request() { FileName = testFile.FileName, Buffer = "" });
                sourceText = await host.Workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
                Assert.Equal("", sourceText.ToString());
            }
        }

        [Fact]
        public async Task UpdateBuffer_AddsNewDocumentsIfNeeded()
        {
            var testFile = new TestFile("test.cs", "class C {}");

            using (var host = CreateOmniSharpHost(testFile))
            {
                await host.Workspace.BufferManager.UpdateBufferAsync(new Request() { FileName = "test2.cs", Buffer = "interface I {}" });

                Assert.Equal(2, host.Workspace.CurrentSolution.GetDocumentIdsWithFilePath("test2.cs").Length);
                var docId = host.Workspace.CurrentSolution.GetDocumentIdsWithFilePath("test2.cs").FirstOrDefault();
                Assert.NotNull(docId);
                var sourceText = await host.Workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
                Assert.Equal("interface I {}", sourceText.ToString());

                docId = host.Workspace.CurrentSolution.GetDocumentIdsWithFilePath(testFile.FileName).FirstOrDefault();
                Assert.NotNull(docId);
                sourceText = await host.Workspace.CurrentSolution.GetDocument(docId).GetTextAsync();
                Assert.Equal("class C {}", sourceText.ToString());
            }
        }

        [Fact]
        public async Task UpdateBuffer_TransientDocumentsDisappearWhenProjectAddsThem()
        {
            var testFile = new TestFile("test.cs", "class C {}");

            using (var host = CreateOmniSharpHost(testFile))
            {
                await host.Workspace.BufferManager.UpdateBufferAsync(new Request() { FileName = "transient.cs", Buffer = "interface I {}" });

                var docIds = host.Workspace.CurrentSolution.GetDocumentIdsWithFilePath("transient.cs");
                Assert.Equal(2, docIds.Length);

                // simulate a project system adding the file for real
                var project1 = host.Workspace.CurrentSolution.Projects.First();
                var document = DocumentInfo.Create(DocumentId.CreateNewId(project1.Id), "transient.cs",
                    loader: TextLoader.From(TextAndVersion.Create(SourceText.From("enum E{}"), VersionStamp.Create())),
                    filePath: "transient.cs");

                // apply changes - this raises workspaceChanged event asynchronously.
                var newSolution = host.Workspace.CurrentSolution.AddDocument(document);
                host.Workspace.TryApplyChanges(newSolution);

                // wait for workspaceChange event to be raised.
                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                while (!cts.IsCancellationRequested)
                {
                    await Task.Yield();
                    docIds = host.Workspace.CurrentSolution.GetDocumentIdsWithFilePath("transient.cs");
                    if (docIds.Count() <= 1) break;
                }

                // assert that only one document with "transient.cs" filePath remains 
                Assert.Single(docIds);

                // assert that the remaining document is part of project1
                project1 = host.Workspace.CurrentSolution.GetProject(project1.Id);
                Assert.Contains(docIds[0], project1.DocumentIds);

                // assert that the remaining document is not transient.
                Assert.False(host.Workspace.BufferManager.IsTransientDocument(docIds[0]));                

                await host.Workspace.BufferManager.UpdateBufferAsync(new Request() { FileName = "transient.cs", Buffer = "enum E {}" });
                var sourceText = await host.Workspace.CurrentSolution.GetDocument(docIds.First()).GetTextAsync();
                Assert.Equal("enum E {}", sourceText.ToString());
                sourceText = await host.Workspace.CurrentSolution.GetDocument(docIds.Last()).GetTextAsync();
                Assert.Equal("enum E {}", sourceText.ToString());
            }
        }
    }
}
