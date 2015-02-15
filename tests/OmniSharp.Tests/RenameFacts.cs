﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using OmniSharp.Filters;
using OmniSharp.Models;

namespace OmniSharp.Tests
{
    public class RenameFacts
    {
        private async Task<RenameResponse> SendRequest(OmnisharpWorkspace workspace, string renameTo, string filename, string fileContent)
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(fileContent);
            var controller = new OmnisharpController(workspace, null);
            var request = new RenameRequest
            {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                RenameTo = renameTo,
                FileName = filename,
                Buffer = fileContent.Replace("$", "")
            };

            var bufferFilter = new UpdateBufferFilter(workspace);
            bufferFilter.OnActionExecuting(TestHelpers.CreateActionExecutingContext(request));

            return await controller.Rename(request);
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

            var workspace = TestHelpers.CreateSimpleWorkspace(fileContent, "test.cs");
            var result = await SendRequest(workspace, "foo", "test.cs", fileContent);

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
        public async Task Rename_UpdatesMultipleDocumentsIfNecessary()
        {
            const string file1 = "public class F$oo {}";
            const string file2 = @"public class Bar {
                                    public Foo Property {get; set;}
                                }";

            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> { { "test1.cs", file1 }, { "test2.cs", file2 } });
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
        public async Task Rename_DoesNotUpdateAnythingWhenDocumentIsNotFound()
        {
            const string fileContent = "class f$oo{}";
            var workspace = TestHelpers.CreateSimpleWorkspace(fileContent);

            var result = await SendRequest(workspace, "xxx", "test.cs", fileContent); 

            Assert.Equal(0, result.Changes.Count());
        }
    }
}