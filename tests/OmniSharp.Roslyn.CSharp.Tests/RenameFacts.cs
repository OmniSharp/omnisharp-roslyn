using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Refactoring;
using TestUtility;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class RenameFacts
    {
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

            const string fileName = "test.cs";

            var markup = MarkupCode.Parse(fileContent);
            var workspace = await TestHelpers.CreateSimpleWorkspace(markup.Code, fileName);
            var result = await PerformRename(workspace, "foo", fileName, markup, applyTextChanges: true);

            var docId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(fileName).First();
            var sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();

            //compare workspace change with response
            Assert.Equal(result.Changes.First().Buffer, sourceText.ToString());

            //check that response refers to correct modified file
            Assert.Equal(result.Changes.First().FileName, fileName);

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

            const string fileName = "test.cs";

            var markup = MarkupCode.Parse(fileContent);
            var workspace = await TestHelpers.CreateSimpleWorkspace(markup.Code, fileName);
            var result = await PerformRename(workspace, "foo", fileName, markup, applyTextChanges: false);

            var docId = workspace.CurrentSolution.GetDocumentIdsWithFilePath("test.cs").First();
            var sourceText = await workspace.CurrentSolution.GetDocument(docId).GetTextAsync();

            // check that the workspace has not been updated
            Assert.Equal(markup.Code, sourceText.ToString());
        }

        [Fact]
        public async Task Rename_UpdatesMultipleDocumentsIfNecessary()
        {
            const string file1 = "public class F$$oo {}";
            const string fileName1 = "test1.cs";

            const string file2 = @"public class Bar {
                                    public Foo Property {get; set;}
                                }";
            const string fileName2 = "test2.cs";

            var file1Markup = MarkupCode.Parse(file1);
            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> { { fileName1, file1 }, { fileName2, file2 } });
            var result = await PerformRename(workspace, "xxx", fileName1, file1Markup);

            var doc1Id = workspace.CurrentSolution.GetDocumentIdsWithFilePath(fileName1).First();
            var doc2Id = workspace.CurrentSolution.GetDocumentIdsWithFilePath(fileName2).First();
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
            const string file1 = "public class F$$oo {}";
            const string fileName1 = "test1.cs";

            const string file2 = @"public class Bar {
                                    public Foo Property {get; set;}
                                }";
            const string fileName2 = "test2.cs";

            var file1Markup = MarkupCode.Parse(file1);
            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> { { fileName1, file1 }, { fileName2, file2 } });
            var result = await PerformRename(workspace, "xxx", fileName1, file1Markup, wantsTextChanges: true);

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
            const string fileName = "test.cs";

            var markup = MarkupCode.Parse(fileContent);
            var workspace = await TestHelpers.CreateSimpleWorkspace();
            var result = await PerformRename(workspace, "xxx", fileName, markup);

            Assert.Equal(1, result.Changes.Count());
            Assert.Equal(fileName, result.Changes.ElementAt(0).FileName);
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
            const string fileName = "test.cs";

            var markup = MarkupCode.Parse(fileContent);
            var workspace = await TestHelpers.CreateSimpleWorkspace(markup.Code, fileName);
            var result = await PerformRename(workspace, "foo", fileName, markup);

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
                    public void Main(bool aBool$$ean)
                    {
                        Console.Write(aBoolean);
                    }
                }";
            const string fileName = "test.cs";

            var markup = MarkupCode.Parse(fileContent);
            var workspace = await TestHelpers.CreateSimpleWorkspace(markup.Code, fileName);
            var result = await PerformRename(workspace, "foo", fileName, markup, wantsTextChanges: true);

            Assert.Equal(1, result.Changes.Count());
            Assert.Equal("test.cs", result.Changes.ElementAt(0).FileName);
            Assert.Equal(2, result.Changes.ElementAt(0).Changes.Count());
        }

        private static async Task<RenameResponse> PerformRename(
            OmnisharpWorkspace workspace,
            string renameTo,
            string fileName,
            MarkupCode fileContent,
            bool wantsTextChanges = false,
            bool applyTextChanges = true)
        {
            var text = SourceText.From(fileContent.Code);
            var line = text.Lines.GetLineFromPosition(fileContent.Position);
            var column = fileContent.Position - line.Start;

            var request = new RenameRequest
            {
                Line = line.LineNumber,
                Column = column,
                RenameTo = renameTo,
                FileName = fileName,
                Buffer = fileContent.Code,
                WantsTextChanges = wantsTextChanges,
                ApplyTextChanges = applyTextChanges
            };

            var controller = new RenameService(workspace);

            await workspace.BufferManager.UpdateBuffer(request);

            return await controller.Handle(request);
        }
    }
}
