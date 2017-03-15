using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Refactoring;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class RenameFacts : AbstractTestFixture
    {
        public RenameFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task Rename_UpdatesWorkspaceAndDocumentText()
        {
            const string fileContent = @"using System;

                        namespace OmniSharp.Models
                        {
                            public class CodeFormat$$Response
                            {
                                public string Buffer { get; set; }
                            }
                        }";


            var testFile = new TestFile("test.cs", fileContent);
            var workspace = await CreateWorkspaceAsync(testFile);
            var result = await PerformRename(workspace, "foo", testFile, applyTextChanges: true);

            var docId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(testFile.FileName).First();
            var sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();

            //compare workspace change with response
            Assert.Equal(result.Changes.First().Buffer, sourceText.ToString());

            //check that response refers to correct modified file
            Assert.Equal(result.Changes.First().FileName, testFile.FileName);

            //check response for change
            Assert.Equal(@"using System;

                        namespace OmniSharp.Models
                        {
                            public class foo
                            {
                                public string Buffer { get; set; }
                            }
                        }", result.Changes.First().Buffer);
        }

        [Fact]
        public async Task Rename_DoesNotUpdatesWorkspace()
        {
            const string fileContent = @"using System;

                        namespace OmniSharp.Models
                        {
                            public class CodeFormat$$Response
                            {
                                public string Buffer { get; set; }
                            }
                        }";

            var testFile = new TestFile("test.cs", fileContent);
            var workspace = await CreateWorkspaceAsync(testFile);
            var result = await PerformRename(workspace, "foo", testFile, applyTextChanges: false);

            var docId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(testFile.FileName).First();
            var sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();

            // check that the workspace has not been updated
            Assert.Equal(testFile.Content.Code, sourceText.ToString());
        }

        [Fact]
        public async Task Rename_UpdatesMultipleDocumentsIfNecessary()
        {
            const string file1 = "public class F$$oo {}";

            const string file2 = @"public class Bar {
                                    public Foo Property {get; set;}
                                }";

            var testFiles = new[]
            {
                new TestFile("test1.cs", file1),
                new TestFile("test2.cs", file2)
            };

            var workspace = await CreateWorkspaceAsync(testFiles);

            var result = await PerformRename(workspace, "xxx", testFiles[0]);

            var doc1Id = workspace.CurrentSolution.GetDocumentIdsWithFilePath(testFiles[0].FileName).First();
            var doc2Id = workspace.CurrentSolution.GetDocumentIdsWithFilePath(testFiles[1].FileName).First();
            var source1Text = await workspace.CurrentSolution.GetDocument(doc1Id).GetTextAsync();
            var source2Text = await workspace.CurrentSolution.GetDocument(doc2Id).GetTextAsync();

            //compare workspace change with response for file 1
            Assert.Equal(source1Text.ToString(), result.Changes.ElementAt(0).Buffer);

            //check that response refers to modified file 1
            Assert.Equal(testFiles[0].FileName, result.Changes.ElementAt(0).FileName);

            //check response for change in file 1
            Assert.Equal(@"public class xxx {}", result.Changes.ElementAt(0).Buffer);

            //compare workspace change with response for file 2
            Assert.Equal(source2Text.ToString(), result.Changes.ElementAt(1).Buffer);

            //check that response refers to modified file 2
            Assert.Equal(testFiles[1].FileName, result.Changes.ElementAt(1).FileName);

            //check response for change in file 2
            Assert.Equal(@"public class Bar {
                                    public xxx Property {get; set;}
                                }", result.Changes.ElementAt(1).Buffer);
        }

        [Fact]
        public async Task Rename_UpdatesMultipleDocumentsIfNecessaryAndProducesTextChangesIfAsked()
        {
            const string file1 = "public class F$$oo {}";
            const string file2 = @"public class Bar {
                                    public Foo Property {get; set;}
                                }";

            var testFiles = new[]
            {
                new TestFile("test1.cs", file1),
                new TestFile("test2.cs", file2)
            };

            var workspace = await CreateWorkspaceAsync(testFiles);
            var result = await PerformRename(workspace, "xxx", testFiles[0], wantsTextChanges: true);

            Assert.Equal(2, result.Changes.Count());
            Assert.Equal(1, result.Changes.ElementAt(0).Changes.Count());

            Assert.Null(result.Changes.ElementAt(0).Buffer);
            Assert.Equal("xxx", result.Changes.ElementAt(0).Changes.First().NewText);
            Assert.Equal(0, result.Changes.ElementAt(0).Changes.First().StartLine);
            Assert.Equal(13, result.Changes.ElementAt(0).Changes.First().StartColumn);
            Assert.Equal(0, result.Changes.ElementAt(0).Changes.First().EndLine);
            Assert.Equal(16, result.Changes.ElementAt(0).Changes.First().EndColumn);

            Assert.Null(result.Changes.ElementAt(1).Buffer);
            Assert.Equal("xxx", result.Changes.ElementAt(1).Changes.First().NewText);
            Assert.Equal(1, result.Changes.ElementAt(1).Changes.First().StartLine);
            Assert.Equal(43, result.Changes.ElementAt(1).Changes.First().StartColumn);
            Assert.Equal(1, result.Changes.ElementAt(1).Changes.First().EndLine);
            Assert.Equal(46, result.Changes.ElementAt(1).Changes.First().EndColumn);
        }

        [Fact]
        public async Task Rename_DoesTheRightThingWhenDocumentIsNotFound()
        {
            const string fileContent = "class f$$oo{}";

            var testFile = new TestFile("test.cs", fileContent);
            var workspace = await CreateWorkspaceAsync();
            var result = await PerformRename(workspace, "xxx", testFile, updateBuffer: true);

            Assert.Equal(1, result.Changes.Count());
            Assert.Equal(testFile.FileName, result.Changes.ElementAt(0).FileName);
        }

        [Fact]
        public async Task Rename_DoesNotExplodeWhenAttemptingToRenameALibrarySymbol()
        {
            const string fileContent = @"
                using System;
                public class Program
                {
                    public static void Main()
                    {
                        Guid.New$$Guid();
                    }
                }";

            var testFile = new TestFile("test.cs", fileContent);
            var workspace = await CreateWorkspaceAsync(testFile);
            var result = await PerformRename(workspace, "foo", testFile);

            Assert.Equal(0, result.Changes.Count());
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task Rename_DoesNotDuplicateRenamesWithMultipleFrameworks()
        {
            const string fileContent = @"
                using System;
                public class Program
                {
                    public void Main(bool aBool$$ean)
                    {
                        Console.Write(aBoolean);
                    }
                }";

            var testFile = new TestFile("test.cs", fileContent);
            var workspace = await CreateWorkspaceAsync(testFile);
            var result = await PerformRename(workspace, "foo", testFile, wantsTextChanges: true);

            Assert.Equal(1, result.Changes.Count());
            Assert.Equal(testFile.FileName, result.Changes.ElementAt(0).FileName);
            Assert.Equal(2, result.Changes.ElementAt(0).Changes.Count());
        }

        private static async Task<RenameResponse> PerformRename(
            OmniSharpWorkspace workspace,
            string renameTo,
            TestFile activeFile,
            bool wantsTextChanges = false,
            bool applyTextChanges = true,
            bool updateBuffer = false)
        {
            var point = activeFile.Content.GetPointFromPosition();

            var request = new RenameRequest
            {
                Line = point.Line,
                Column = point.Offset,
                RenameTo = renameTo,
                FileName = activeFile.FileName,
                Buffer = activeFile.Content.Code,
                WantsTextChanges = wantsTextChanges,
                ApplyTextChanges = applyTextChanges
            };

            var controller = new RenameService(workspace);

            if (updateBuffer)
            {
                await workspace.BufferManager.UpdateBufferAsync(request);
            }

            return await controller.Handle(request);
        }
    }
}
