using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.Rename;
using OmniSharp.Roslyn.CSharp.Services.Refactoring;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class RenameFacts : AbstractSingleRequestHandlerTestFixture<RenameService>
    {
        public RenameFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.Rename;

        [Fact]
        public async Task Rename_UpdatesWorkspaceAndDocumentText()
        {
            const string code = @"
using System;

namespace OmniSharp.Models
{
    public class CodeFormat$$Response
    {
        public string Buffer { get; set; }
    }
}";

            const string expectedCode = @"
using System;

namespace OmniSharp.Models
{
    public class foo
    {
        public string Buffer { get; set; }
    }
}";

            var testFile = new TestFile("test.cs", code);

            using (var host = CreateOmniSharpHost(testFile))
            {
                await ValidateRename(host, testFile, expectedCode, async () => await PerformRename(host, testFile, "foo", applyTextChanges: true));
            }
        }

        [Fact]
        public async Task Rename_DoesNotUpdatesWorkspace()
        {
            const string fileContent = @"
using System;

namespace OmniSharp.Models
{
    public class CodeFormat$$Response
    {
        public string Buffer { get; set; }
    }
}";

            var testFile = new TestFile("test.cs", fileContent);
            using (var host = CreateOmniSharpHost(testFile))
            {
                var result = await PerformRename(host, testFile, "foo", applyTextChanges: false);

                var solution = host.Workspace.CurrentSolution;
                var documentId = solution.GetDocumentIdsWithFilePath(testFile.FileName).First();
                var document = solution.GetDocument(documentId);
                var sourceText = await document.GetTextAsync();

                // check that the workspace has not been updated
                Assert.Equal(testFile.Content.Code, sourceText.ToString());
            }
        }

        [Fact]
        public async Task Rename_UpdatesMultipleDocumentsIfNecessary()
        {
            const string code1 = "public class F$$oo {}";

            const string code2 = @"
public class Bar {
    public Foo Property {get; set;}
}";

            const string expectedCode = @"
public class Bar {
    public xxx Property {get; set;}
}";

            var testFiles = new[]
            {
                new TestFile("test1.cs", code1),
                new TestFile("test2.cs", code2)
            };

            using (var host = CreateOmniSharpHost(testFiles))
            {
                var result = await PerformRename(host, testFiles, "xxx");

                var solution = host.Workspace.CurrentSolution;
                var documentId1 = solution.GetDocumentIdsWithFilePath(testFiles[0].FileName).First();
                var document1 = solution.GetDocument(documentId1);
                var sourceText1 = await document1.GetTextAsync();
                var documentId2 = solution.GetDocumentIdsWithFilePath(testFiles[1].FileName).First();
                var document2 = solution.GetDocument(documentId2);
                var sourceText2 = await document2.GetTextAsync();

                var changes = result.Changes.ToArray();

                //compare workspace change with response for file 1
                Assert.Equal(sourceText1.ToString(), changes[0].Buffer);

                //check that response refers to modified file 1
                Assert.Equal(testFiles[0].FileName, changes[0].FileName);

                //check response for change in file 1
                Assert.Equal(@"public class xxx {}", changes[0].Buffer);

                //compare workspace change with response for file 2
                Assert.Equal(sourceText2.ToString(), changes[1].Buffer);

                //check that response refers to modified file 2
                Assert.Equal(testFiles[1].FileName, changes[1].FileName);

                //check response for change in file 2
                Assert.Equal(expectedCode, changes[1].Buffer);
            }
        }

        [Fact]
        public async Task Rename_UpdatesMultipleDocumentsIfNecessaryAndProducesTextChangesIfAsked()
        {
            const string code1 = "public class F$$oo {}";
            const string code2 = @"
public class Bar {
    public Foo Property {get; set;}
}";

            var testFiles = new[]
            {
                new TestFile("test1.cs", code1),
                new TestFile("test2.cs", code2)
            };

            var result = await PerformRename(testFiles, "xxx", wantsTextChanges: true);
            var changes = result.Changes.ToArray();

            Assert.Equal(2, changes.Length);
            Assert.Single(changes[0].Changes);

            Assert.Null(changes[0].Buffer);
            Assert.Equal("xxx", changes[0].Changes.First().NewText);
            Assert.Equal(0, changes[0].Changes.First().StartLine);
            Assert.Equal(13, changes[0].Changes.First().StartColumn);
            Assert.Equal(0, changes[0].Changes.First().EndLine);
            Assert.Equal(16, changes[0].Changes.First().EndColumn);

            Assert.Null(changes[1].Buffer);
            Assert.Equal("xxx", changes[1].Changes.First().NewText);
            Assert.Equal(2, changes[1].Changes.First().StartLine);
            Assert.Equal(11, changes[1].Changes.First().StartColumn);
            Assert.Equal(2, changes[1].Changes.First().EndLine);
            Assert.Equal(14, changes[1].Changes.First().EndColumn);
        }

        [Fact]
        public async Task Rename_DoesTheRightThingWhenDocumentIsNotFound()
        {
            const string fileContent = "class f$$oo{}";

            var testFile = new TestFile("test.cs", fileContent);

            // Note: We intentionally aren't including the TestFile in the host.
            using (var host = CreateEmptyOmniSharpHost())
            {
                var result = await PerformRename(host, testFile, "xxx", updateBuffer: true);

                var changes = result.Changes.ToArray();

                Assert.Single(changes);
                Assert.Equal(testFile.FileName, changes[0].FileName);
            }
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
            var result = await PerformRename(testFile, "foo");

            Assert.Empty(result.Changes);
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
            var result = await PerformRename(testFile, "foo", wantsTextChanges: true);

            var changes = result.Changes.ToArray();

            Assert.Single(changes);
            Assert.Equal(testFile.FileName, changes[0].FileName);
            Assert.Equal(2, changes[0].Changes.Count());
        }

        [Fact]
        public async Task Rename_CanRenameInComments()
        {
            const string code = @"
using System;

namespace ConsoleApplication
{
    /// <summary>  
    ///  This program performs an important work and calls Bar.  
    /// </summary> 
    public class Program
    {
        static void Ba$$r() {}
    }
}";

            const string expectedCode = @"
using System;

namespace ConsoleApplication
{
    /// <summary>  
    ///  This program performs an important work and calls Foo.  
    /// </summary> 
    public class Program
    {
        static void Foo() {}
    }
}";

            var testFile = new TestFile("test.cs", code);
            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["RenameOptions:RenameInComments"] = "true"
            }))
            {
                await ValidateRename(host, testFile, expectedCode, async () => await PerformRename(host, testFile, "Foo", applyTextChanges: true));
            }
        }

        [Fact]
        public async Task Rename_CanRenameInOverloads()
        {
            const string code = @"
public class Foo
{
    public void Do$$Stuff() {}

    public void DoStuff(int foo) {}
    
    public void DoStuff(int foo, int bar) {}
}";

            const string expectedCode = @"
public class Foo
{
    public void DoFunnyStuff() {}

    public void DoFunnyStuff(int foo) {}
    
    public void DoFunnyStuff(int foo, int bar) {}
}";

            var testFile = new TestFile("test.cs", code);
            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["RenameOptions:RenameOverloads"] = "true"
            }))
            {
                await ValidateRename(host, testFile, expectedCode, async () => await PerformRename(host, testFile, "DoFunnyStuff", applyTextChanges: true));
            }
        }

        [Fact]
        public async Task Rename_CanRenameInStrings()
        {
            const string code = @"
namespace ConsoleApplication
{
    public class Ba$$r
    {
        public static string Name = ""Bar"";
    }
}";

            const string expectedCode = @"
namespace ConsoleApplication
{
    public class Foo
    {
        public static string Name = ""Foo"";
    }
}";

            var testFile = new TestFile("test.cs", code);
            using (var host = CreateOmniSharpHost(new[] { testFile }, new Dictionary<string, string>
            {
                ["RenameOptions:RenameInStrings"] = "true"
            }))
            {
                await ValidateRename(host, testFile, expectedCode, async () => await PerformRename(host, testFile, "Foo", applyTextChanges: true));
            }
        }

        private async Task ValidateRename(OmniSharpTestHost host, TestFile testFile, string expectedCode, Func<Task<RenameResponse>> rename)
        {
            var result = await rename();

            var solution = host.Workspace.CurrentSolution;
            var documentId = solution.GetDocumentIdsWithFilePath(testFile.FileName).First();
            var document = solution.GetDocument(documentId);
            var sourceText = await document.GetTextAsync();

            var change = result.Changes.Single();

            // compare workspace change with response
            Assert.Equal(change.Buffer, sourceText.ToString());

            // check that response refers to correct modified file
            Assert.Equal(change.FileName, testFile.FileName);

            // check response for change
            Assert.Equal(expectedCode, change.Buffer);
        }

        private Task<RenameResponse> PerformRename(
            TestFile testFile, string renameTo,
            bool wantsTextChanges = false,
            bool applyTextChanges = true,
            bool updateBuffer = false)
        {
            return PerformRename(new[] { testFile }, renameTo, wantsTextChanges, applyTextChanges, updateBuffer);
        }

        private async Task<RenameResponse> PerformRename(
            TestFile[] testFiles, string renameTo,
            bool wantsTextChanges = false,
            bool applyTextChanges = true,
            bool updateBuffer = false)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFiles);
            return await PerformRename(SharedOmniSharpTestHost, testFiles, renameTo, wantsTextChanges, applyTextChanges, updateBuffer);
        }

        private Task<RenameResponse> PerformRename(
            OmniSharpTestHost host, TestFile testFile, string renameTo,
            bool wantsTextChanges = false,
            bool applyTextChanges = true,
            bool updateBuffer = false)
        {
            return PerformRename(host, new[] { testFile }, renameTo, wantsTextChanges, applyTextChanges, updateBuffer);
        }

        private async Task<RenameResponse> PerformRename(
            OmniSharpTestHost host, TestFile[] testFiles, string renameTo,
            bool wantsTextChanges = false,
            bool applyTextChanges = true,
            bool updateBuffer = false)
        {
            var activeFile = testFiles.Single(tf => tf.Content.HasPosition);
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

            var requestHandler = GetRequestHandler(host);

            if (updateBuffer)
            {
                await host.Workspace.BufferManager.UpdateBufferAsync(request);
            }

            return await requestHandler.Handle(request);
        }
    }
}
