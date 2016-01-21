using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Refactoring;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class RenameFacts
    {
        private async Task<RenameResponse> SendRequest(OmnisharpWorkspace workspace,
                                                       string renameTo,
                                                       string filename,
                                                       string fileContent,
                                                       bool wantsTextChanges = false,
                                                       bool applyTextChanges = true)
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(fileContent);
            var controller = new RenameService(workspace);
            var request = new RenameRequest
            {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                RenameTo = renameTo,
                FileName = filename,
                Buffer = fileContent.Replace("$", ""),
                WantsTextChanges = wantsTextChanges,
                ApplyTextChanges = applyTextChanges
            };

            await workspace.BufferManager.UpdateBuffer(request);

            return await controller.Handle(request);
        }

        [Fact]
        public async Task Rename_UpdatesWorkspaceAndDocumentText()
        {
            const string fileContent = @"using System;

                        namespace OmniSharp.Models
                        {
                            public class CodeFormat$Response
                            {
                                public string Buffer { get; set; }
                            }
                        }";

            var workspace = await TestHelpers.CreateSimpleWorkspace(fileContent, "test.cs");
            var result = await SendRequest(workspace, "foo", "test.cs", fileContent, applyTextChanges: true);

            var docId = workspace.CurrentSolution.GetDocumentIdsWithFilePath("test.cs").First();
            var sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();

            //compare workspace change with response
            Assert.Equal(result.Changes.First().Buffer, sourceText.ToString());

            //check that response refers to correct modified file
            Assert.Equal(result.Changes.First().FileName, "test.cs");

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
                            public class CodeFormat$Response
                            {
                                public string Buffer { get; set; }
                            }
                        }";

            var workspace = await TestHelpers.CreateSimpleWorkspace(fileContent, "test.cs");
            var result = await SendRequest(workspace, "foo", "test.cs", fileContent, applyTextChanges: false);

            var docId = workspace.CurrentSolution.GetDocumentIdsWithFilePath("test.cs").First();
            var sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();


            // check that the workspace has not been updated
            Assert.Equal(fileContent.Replace("$", ""), sourceText.ToString());
        }

        [Fact]
        public async Task Rename_UpdatesMultipleDocumentsIfNecessary()
        {
            const string file1 = "public class F$oo {}";
            const string file2 = @"public class Bar {
                                    public Foo Property {get; set;}
                                }";

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> { { "test1.cs", file1 }, { "test2.cs", file2 } });
            var result = await SendRequest(workspace, "xxx", "test1.cs", file1);

            var doc1Id = workspace.CurrentSolution.GetDocumentIdsWithFilePath("test1.cs").First();
            var doc2Id = workspace.CurrentSolution.GetDocumentIdsWithFilePath("test2.cs").First();
            var source1Text = await workspace.CurrentSolution.GetDocument(doc1Id).GetTextAsync();
            var source2Text = await workspace.CurrentSolution.GetDocument(doc2Id).GetTextAsync();

            //compare workspace change with response for file 1
            Assert.Equal(result.Changes.ElementAt(0).Buffer, source1Text.ToString());

            //check that response refers to modified file 1
            Assert.Equal(result.Changes.ElementAt(0).FileName, "test1.cs");

            //check response for change in file 1
            Assert.Equal(@"public class xxx {}", result.Changes.ElementAt(0).Buffer);

            //compare workspace change with response for file 2
            Assert.Equal(result.Changes.ElementAt(1).Buffer, source2Text.ToString());

            //check that response refers to modified file 2
            Assert.Equal(result.Changes.ElementAt(1).FileName, "test2.cs");

            //check response for change in file 2
            Assert.Equal(@"public class Bar {
                                    public xxx Property {get; set;}
                                }", result.Changes.ElementAt(1).Buffer);
        }

        [Fact]
        public async Task Rename_UpdatesMultipleDocumentsIfNecessaryAndProducesTextChangesIfAsked()
        {
            const string file1 = "public class F$oo {}";
            const string file2 = @"public class Bar {
                                    public Foo Property {get; set;}
                                }";

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> { { "test1.cs", file1 }, { "test2.cs", file2 } });
            var result = await SendRequest(workspace, "xxx", "test1.cs", file1, true);

            Assert.Equal(2, result.Changes.Count());
            Assert.Equal(1, result.Changes.ElementAt(0).Changes.Count());

            Assert.Null(result.Changes.ElementAt(0).Buffer);
            Assert.Equal("xxx", result.Changes.ElementAt(0).Changes.First().NewText);
            Assert.Equal(1, result.Changes.ElementAt(0).Changes.First().StartLine);
            Assert.Equal(14, result.Changes.ElementAt(0).Changes.First().StartColumn);
            Assert.Equal(1, result.Changes.ElementAt(0).Changes.First().EndLine);
            Assert.Equal(17, result.Changes.ElementAt(0).Changes.First().EndColumn);

            Assert.Null(result.Changes.ElementAt(1).Buffer);
            Assert.Equal("xxx", result.Changes.ElementAt(1).Changes.First().NewText);
            Assert.Equal(2, result.Changes.ElementAt(1).Changes.First().StartLine);
            Assert.Equal(44, result.Changes.ElementAt(1).Changes.First().StartColumn);
            Assert.Equal(2, result.Changes.ElementAt(1).Changes.First().EndLine);
            Assert.Equal(47, result.Changes.ElementAt(1).Changes.First().EndColumn);
        }

        [Fact]
        public async Task Rename_DoesTheRightThingWhenDocumentIsNotFound()
        {
            const string fileContent = "class f$oo{}";
            var workspace = await TestHelpers.CreateSimpleWorkspace(fileContent);

            var result = await SendRequest(workspace, "xxx", "test.cs", fileContent);

            Assert.Equal(1, result.Changes.Count());
            Assert.Equal("test.cs", result.Changes.ElementAt(0).FileName);
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
                        Guid.New$Guid*();
                    }
                }";

            var workspace = await TestHelpers.CreateSimpleWorkspace(fileContent, "test.cs");
            var result = await SendRequest(workspace, "foo", "test.cs", fileContent);

            Assert.Equal(0, result.Changes.Count());
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task Rename_DoesNotDuplicateRenamesWithMultipleFrameowrks()
        {
            const string fileContent = @"
                using System;
                public class Program
                {
                    public void Main(bool aBool$ean)
                    {
                        Console.Write(aBoolean);
                    }
                }";

            var workspace = await TestHelpers.CreateSimpleWorkspace(fileContent, "test.cs");
            var result = await SendRequest(workspace, "foo", "test.cs", fileContent, true);

            Assert.Equal(1, result.Changes.Count());
            Assert.Equal("test.cs", result.Changes.ElementAt(0).FileName);
            Assert.Equal(2, result.Changes.ElementAt(0).Changes.Count());
        }
    }
}
