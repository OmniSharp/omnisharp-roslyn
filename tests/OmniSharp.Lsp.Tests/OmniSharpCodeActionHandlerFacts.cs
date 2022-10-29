using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Models.V2;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OmniSharp.Lsp.Tests
{
    public class OmniSharpCodeActionHandlerFacts : AbstractLanguageServerTestBase
    {
        public OmniSharpCodeActionHandlerFacts(ITestOutputHelper output)
            : base(output) { }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_get_code_actions_from_roslyn(bool roslynAnalyzersEnabled)
        {
            const string code =
                @"public class Class1
                    {
                        public void Whatever()
                        {
                            Gu[||]id.NewGuid();
                        }
                    }";

            var refactorings = await FindRefactoringNamesAsync(code, roslynAnalyzersEnabled);
            Assert.Contains("using System;", refactorings);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_get_code_actions_from_external_source(bool roslynAnalyzersEnabled)
        {
            await Restart(configurationData: new Dictionary<string, string>
            {
                {"RoslynExtensionsOptions:LocationPaths:0", TestAssets.Instance.TestBinariesFolder},
            });
            const string code =
                @"
                    using System.Threading.Tasks;
                    public class Class1
                    {
                        public async Task Whatever()
                        {
                            awa[||]it FooAsync();
                        }

                        public Task FooAsync() => return Task.FromResult(0);
                    }";

            var refactorings = await FindRefactoringsAsync(code,
                TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled));

            Assert.NotEmpty(refactorings);
            Assert.Contains("Add ConfigureAwait(false)", refactorings.Select(x => x.Title));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_remove_unnecessary_usings(bool roslynAnalyzersEnabled)
        {
            const string code =
                @"using MyNamespace3;
                using MyNamespace4;
                using MyNamespace2;
                using System;
                u[||]sing MyNamespace1;
                public class c {public c() {Guid.NewGuid();}}";

            const string expected =
                @"using System;
                public class c {public c() {Guid.NewGuid();}}";

            var response =
                (await RunRefactoringAsync(code, "Remove Unnecessary Usings",
                    isAnalyzersEnabled: roslynAnalyzersEnabled)).Single();
            var updatedText = await OmniSharpTestHost.Workspace.GetDocument(response.FileName).GetTextAsync(CancellationToken);
            AssertUtils.AssertIgnoringIndent(expected, updatedText.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_get_ranged_code_action(bool roslynAnalyzersEnabled)
        {
            const string code =
                @"public class Class1
                {
                    public void Whatever()
                    {
                        [|Console.Write(""should be using System;"");|]
                    }
                }";

            var refactorings = await FindRefactoringNamesAsync(code, roslynAnalyzersEnabled);
            Assert.Contains("Extract method", refactorings);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Returns_ordered_code_actions(bool roslynAnalyzersEnabled)
        {
            const string code =
                @"public class Class1
                {
                    public void Whatever()
                    {
                        [|Console.Write(""should be using System;"");|]
                    }
                }";

            List<string> expected = roslynAnalyzersEnabled
                ? new List<string>
                {
                    "Fix formatting",
                    "using System;",
#if NETCOREAPP
                    "using Internal;",
                    "Fully qualify 'Console' -> Internal.Console",
                    "Fully qualify 'Console' -> System.Console",
#else
                    "System.Console",
#endif
                    "Generate variable 'Console' -> Generate property 'Console'",
                    "Generate variable 'Console' -> Generate field 'Console'",
                    "Generate variable 'Console' -> Generate read-only field 'Console'",
                    "Generate variable 'Console' -> Generate local 'Console'",
                    "Generate variable 'Console' -> Generate parameter 'Console'",
                    "Generate type 'Console' -> Generate class 'Console' in new file",
                    "Generate type 'Console' -> Generate class 'Console'",
                    "Generate type 'Console' -> Generate nested class 'Console'",
                    "Extract local function",
                    "Extract method",
                    "Introduce local for 'Console.Write(\"should be using System;\")'"
                }
                : new List<string>
                {
                    "using System;",
#if NETCOREAPP
                    "using Internal;",
                    "Fully qualify 'Console' -> Internal.Console",
                    "Fully qualify 'Console' -> System.Console",
#else
                    "System.Console",
#endif
                    "Generate variable 'Console' -> Generate property 'Console'",
                    "Generate variable 'Console' -> Generate field 'Console'",
                    "Generate variable 'Console' -> Generate read-only field 'Console'",
                    "Generate variable 'Console' -> Generate local 'Console'",
                    "Generate variable 'Console' -> Generate parameter 'Console'",
                    "Generate type 'Console' -> Generate class 'Console' in new file",
                    "Generate type 'Console' -> Generate class 'Console'",
                    "Generate type 'Console' -> Generate nested class 'Console'",
                    "Extract local function",
                    "Extract method",
                    "Introduce local for 'Console.Write(\"should be using System;\")'"
                };

            // test has intermittent failures during startup.
            await TestHelpers.WaitUntil(async () =>
            {
                try
                {
                    var refactorings = await FindRefactoringNamesAsync(code, roslynAnalyzersEnabled);
                    Assert.Equal(expected.OrderBy(x => x).Skip(8), refactorings.OrderBy(x => x).Skip(8));
                    return true;
                }
                catch (EqualException)
                {
                    return false;
                }
            }, timeout: 30000);

        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_extract_method(bool roslynAnalyzersEnabled)
        {
            const string code =
                @"public class Class1
                {
                    public void Whatever()
                    {
                        [|Console.Write(""should be using System;"");|]
                    }
                }";
            const string expected =
                @"public class Class1
                {
                    public void Whatever()
                    {
                        NewMethod();
                    }

                    private static void NewMethod()
                    {
                        Console.Write(""should be using System;"");
                    }
                }";
            var response =
                (await RunRefactoringAsync(code, "Extract Method", isAnalyzersEnabled: roslynAnalyzersEnabled))
                .Single();
            var updatedText = await OmniSharpTestHost.Workspace.GetDocument(response.FileName).GetTextAsync(CancellationToken);
            AssertUtils.AssertIgnoringIndent(expected, updatedText.ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_generate_type_and_return_name_of_new_file(bool roslynAnalyzersEnabled)
        {
            await Configuration.Update("omnisharp",
                TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled));
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMissingType");
            var project = await AddProjectToWorkspace(testProject);
            var document = project.Documents.First();

            var codeActions = await Client.RequestCodeAction(new CodeActionParams()
            {
                Context = new CodeActionContext(),
                TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(document.FilePath)),
                Range = new Range((8, 12), (8, 12)),
            }, CancellationToken);

            var codeActionOrCommand = codeActions
                .SingleOrDefault(ca => ca.CodeAction.Title == "Generate type 'Z' -> Generate class 'Z' in new file");

            Assert.NotNull(codeActionOrCommand);
            Assert.True(codeActionOrCommand.IsCodeAction);

            var codeAction = codeActionOrCommand.CodeAction;

            var resolvedCodeAction = await Client.ResolveCodeAction(codeAction, CancellationToken);

            var change = resolvedCodeAction.Edit.DocumentChanges.SingleOrDefault();
            Assert.True(change.IsTextDocumentEdit);

            var textEdit = change.TextDocumentEdit.Edits.SingleOrDefault();

            const string expected = @"namespace ConsoleApplication
{
    internal class Z
    {
    }
}";
            Assert.Equal(expected.Replace("\r\n", "\n"), textEdit.NewText);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_send_rename_and_fileOpen_responses_when_codeAction_renames_file(bool roslynAnalyzersEnabled)
        {
            await Restart(TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled));
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMismatchedFileName");
            var project = await AddProjectToWorkspace(testProject);
            var document = project.Documents.First();

            var codeActions = await Client.RequestCodeAction(new CodeActionParams()
            {
                Context = new CodeActionContext(),
                TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(document.FilePath)),
                Range = new Range((4, 10), (4, 10)),
            }, CancellationToken);

            var codeActionOrCommand = codeActions
                .SingleOrDefault(ca => ca.CodeAction.Title == "Rename file to Class1.cs");

            Assert.NotNull(codeActionOrCommand);
            Assert.True(codeActionOrCommand.IsCodeAction);

            var codeAction = codeActionOrCommand.CodeAction;

            var resolvedCodeAction = await Client.ResolveCodeAction(codeAction, CancellationToken);

            var change = resolvedCodeAction.Edit.DocumentChanges.SingleOrDefault();

            Assert.True(change.IsRenameFile);

            var expected = DocumentUri.FromFileSystemPath(Path.Combine(Path.GetDirectoryName(document.FilePath), "Class1.cs"));
            Assert.Equal(expected.GetFileSystemPath(), change.RenameFile.NewUri.GetFileSystemPath());
        }

        private async Task<IEnumerable<TestFile>> RunRefactoringAsync(string code, string refactoringName, bool isAnalyzersEnabled = true)
        {
            await Restart(TestHelpers.GetConfigurationDataWithAnalyzerConfig(isAnalyzersEnabled));

            var bufferPath =
                $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}buffer.cs";
            var testFile = new TestFile(bufferPath, code);
            OmniSharpTestHost.AddFilesToWorkspace(testFile);

            var project = OmniSharpTestHost.Workspace.CurrentSolution.Projects.Single();
            var document = project.Documents.First();

            var span = testFile.Content.GetSpans().Single();
            var range = GetSelection(testFile.Content.GetRangeFromSpan(span));

            // Request CodeAction
            var codeActions = await Client.RequestCodeAction(new CodeActionParams()
            {
                Context = new CodeActionContext(),
                TextDocument = new TextDocumentIdentifier(DocumentUri.FromFileSystemPath(document.FilePath)),
                Range = LanguageServerProtocol.Helpers.ToRange(range),
            }, CancellationToken);

            // Locate CodeAction
            var codeAction = codeActions
                .Where(ca => ca.IsCodeAction)
                .Select(ca => ca.CodeAction)
                .SingleOrDefault(ca => ca.Title.Equals(refactoringName, StringComparison.OrdinalIgnoreCase));

            // Resolve CodeAction
            var resolvedCodeAction = await Client.ResolveCodeAction(codeAction, CancellationToken);

            // Apply CodeAction
            await Server.SendRequest(new ApplyWorkspaceEditParams()
            {
                Label = codeAction.Title,
                Edit = resolvedCodeAction.Edit
            }, CancellationToken);

            return new[] { testFile };
        }

        private async Task<IEnumerable<string>> FindRefactoringNamesAsync(string code, bool isAnalyzersEnabled = true)
        {
            var codeActions = await FindRefactoringsAsync(code,
                TestHelpers.GetConfigurationDataWithAnalyzerConfig(isAnalyzersEnabled));

            return codeActions.Select(a => a.Title);
        }

        private async Task<IEnumerable<CodeAction>> FindRefactoringsAsync(string code,
            IConfiguration configurationData = null)
        {
            var bufferPath =
                $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}buffer.cs";
            var testFile = new TestFile(bufferPath, code);
            OmniSharpTestHost.AddFilesToWorkspace(testFile);
            await Configuration.Update("csharp", configurationData);

            var span = testFile.Content.GetSpans().Single();
            var range = GetSelection(testFile.Content.GetRangeFromSpan(span));
            var response = await Client.RequestCodeAction(new CodeActionParams()
            {
                Context = new CodeActionContext() { },
                Range = LanguageServerProtocol.Helpers.ToRange(range),
                TextDocument = new TextDocumentIdentifier(bufferPath)
            }, CancellationToken);

            return response.Where(z => z.IsCodeAction).Select(z => z.CodeAction);
        }

        private static Models.V2.Range GetSelection(TextRange range)
        {
            if (range.IsEmpty)
            {
                return null;
            }

            return new Models.V2.Range
            {
                Start = new Point { Line = range.Start.Line, Column = range.Start.Offset },
                End = new Point { Line = range.End.Line, Column = range.End.Offset }
            };
        }
    }
}
