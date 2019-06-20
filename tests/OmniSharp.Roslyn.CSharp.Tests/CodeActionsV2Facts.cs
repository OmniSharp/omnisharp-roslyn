using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.V2;
using OmniSharp.Models.V2.CodeActions;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class CodeActionsV2Facts : AbstractTestFixture
    {
        private readonly string BufferPath = $"{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}buffer.cs";

        public CodeActionsV2Facts(ITestOutputHelper output)
            : base(output)
        {
        }

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

            var configuration = new Dictionary<string, string>
            {
                { "RoslynExtensionsOptions:LocationPaths:0", TestAssets.Instance.TestBinariesFolder },
            };

            var refactorings = await FindRefactoringsAsync(code,
                TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled, existingConfiguration: configuration));

            Assert.NotEmpty(refactorings);
            Assert.Contains("Add ConfigureAwait(false)", refactorings.Select(x => x.Name));
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

            var response = await RunRefactoringAsync(code, "Remove Unnecessary Usings", roslynAnalyzersEnabled: roslynAnalyzersEnabled);
            AssertIgnoringIndent(expected, ((ModifiedFileResponse)response.Changes.First()).Buffer);
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
            Assert.Contains("Extract Method", refactorings);
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

            var refactorings = await FindRefactoringNamesAsync(code, roslynAnalyzersEnabled);

            List<string> expected = roslynAnalyzersEnabled ? new List<string>
            {
                "Fix formatting",
                "using System;",
                "System.Console",
                "Generate variable 'Console' -> Generate property 'Class1.Console'",
                "Generate variable 'Console' -> Generate field 'Class1.Console'",
                "Generate variable 'Console' -> Generate read-only field 'Class1.Console'",
                "Generate variable 'Console' -> Generate local 'Console'",
                "Generate variable 'Console' -> Generate parameter 'Console'",
                "Generate type 'Console' -> Generate class 'Console' in new file",
                "Generate type 'Console' -> Generate class 'Console'",
                "Generate type 'Console' -> Generate nested class 'Console'",
                "Extract Method"
            } : new List<string>
            {
                "using System;",
                "System.Console",
                "Generate variable 'Console' -> Generate property 'Class1.Console'",
                "Generate variable 'Console' -> Generate field 'Class1.Console'",
                "Generate variable 'Console' -> Generate read-only field 'Class1.Console'",
                "Generate variable 'Console' -> Generate local 'Console'",
                "Generate variable 'Console' -> Generate parameter 'Console'",
                "Generate type 'Console' -> Generate class 'Console' in new file",
                "Generate type 'Console' -> Generate class 'Console'",
                "Generate type 'Console' -> Generate nested class 'Console'",
                "Extract Method"
            };

            Assert.Equal(expected, refactorings);
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
            var response = await RunRefactoringAsync(code, "Extract Method", roslynAnalyzersEnabled: roslynAnalyzersEnabled);
            AssertIgnoringIndent(expected, ((ModifiedFileResponse)response.Changes.First()).Buffer);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_generate_type_and_return_name_of_new_file(bool roslynAnalyzersEnabled)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMissingType"))
            using (var host = CreateOmniSharpHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled)))
            {
                var requestHandler = host.GetRequestHandler<RunCodeActionService>(OmniSharpEndpoints.V2.RunCodeAction);
                var document = host.Workspace.CurrentSolution.Projects.First().Documents.First();
                var buffer = await document.GetTextAsync();
                var path = document.FilePath;

                var request = new RunCodeActionRequest
                {
                    Line = 8,
                    Column = 12,
                    FileName = path,
                    Buffer = buffer.ToString(),
                    Identifier = "Generate class 'Z' in new file",
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true
                };

                var response = await requestHandler.Handle(request);
                var changes = response.Changes.ToArray();
                Assert.Equal(2, changes.Length);
                Assert.NotNull(changes[0].FileName);

                Assert.True(File.Exists(changes[0].FileName));
                Assert.Equal(@"namespace ConsoleApplication
{
    internal class Z
    {
    }
}".Replace("\r\n", "\n"), ((ModifiedFileResponse)changes[0]).Changes.First().NewText);

                Assert.NotNull(changes[1].FileName);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_send_rename_and_fileOpen_responses_when_codeAction_renames_file(bool roslynAnalyzersEnabled)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithMismatchedFileName"))
            using (var host = CreateOmniSharpHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled)))
            {
                var requestHandler = host.GetRequestHandler<RunCodeActionService>(OmniSharpEndpoints.V2.RunCodeAction);
                var document = host.Workspace.CurrentSolution.Projects.First().Documents.First();
                var buffer = await document.GetTextAsync();
                var path = document.FilePath;

                var request = new RunCodeActionRequest
                {
                    Line = 4,
                    Column = 10,
                    FileName = path,
                    Buffer = buffer.ToString(),
                    Identifier = "Rename file to Class1.cs",
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true
                };

                var response = await requestHandler.Handle(request);
                var changes = response.Changes.ToArray();
                Assert.Equal(2, changes.Length);
                Assert.Equal(FileModificationType.Renamed, changes[0].ModificationType);
                Assert.Contains("Class1.cs", ((RenamedFileResponse)changes[0]).NewFileName);
                Assert.False(File.Exists(((RenamedFileResponse)changes[0]).FileName), "The old renamed file exists - even though it should not.");
                Assert.True(File.Exists(((RenamedFileResponse)changes[0]).NewFileName), "The new renamed file doesn't exist - even though it should.");
                Assert.Equal(FileModificationType.Opened, changes[1].ModificationType);
            }
        }

        private static void AssertIgnoringIndent(string expected, string actual)
        {
            Assert.Equal(TrimLines(expected), TrimLines(actual), false, true, true);
        }

        private static string TrimLines(string source)
        {
            return string.Join("\n", source.Split('\n').Select(s => s.Trim()));
        }

        private async Task<RunCodeActionResponse> RunRefactoringAsync(string code, string refactoringName, bool wantsChanges = false, bool roslynAnalyzersEnabled = false)
        {
            var refactorings = await FindRefactoringsAsync(code, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled));
            Assert.Contains(refactoringName, refactorings.Select(a => a.Name));

            var identifier = refactorings.First(action => action.Name.Equals(refactoringName)).Identifier;
            return await RunRefactoringsAsync(code, identifier, wantsChanges);
        }

        private async Task<IEnumerable<string>> FindRefactoringNamesAsync(string code, bool roslynAnalyzersEnabled = false)
        {
            var codeActions = await FindRefactoringsAsync(code, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled));

            return codeActions.Select(a => a.Name);
        }

        private async Task<IEnumerable<OmniSharpCodeAction>> FindRefactoringsAsync(string code, IDictionary<string, string> configurationData = null)
        {
            var testFile = new TestFile(BufferPath, code);

            using (var host = CreateOmniSharpHost(new[] { testFile }, configurationData))
            {
                var requestHandler = host.GetRequestHandler<GetCodeActionsService>(OmniSharpEndpoints.V2.GetCodeActions);

                var span = testFile.Content.GetSpans().Single();
                var range = testFile.Content.GetRangeFromSpan(span);

                var request = new GetCodeActionsRequest
                {
                    Line = range.Start.Line,
                    Column = range.Start.Offset,
                    FileName = BufferPath,
                    Buffer = testFile.Content.Code,
                    Selection = GetSelection(range),
                };

                var response = await requestHandler.Handle(request);

                return response.CodeActions;
            }
        }

        private async Task<RunCodeActionResponse> RunRefactoringsAsync(string code, string identifier, bool wantsChanges = false)
        {
            var testFile = new TestFile(BufferPath, code);

            using (var host = CreateOmniSharpHost(testFile))
            {
                var requestHandler = host.GetRequestHandler<RunCodeActionService>(OmniSharpEndpoints.V2.RunCodeAction);

                var span = testFile.Content.GetSpans().Single();
                var range = testFile.Content.GetRangeFromSpan(span);

                var request = new RunCodeActionRequest
                {
                    Line = range.Start.Line,
                    Column = range.Start.Offset,
                    Selection = GetSelection(range),
                    FileName = BufferPath,
                    Buffer = testFile.Content.Code,
                    Identifier = identifier,
                    WantsTextChanges = wantsChanges,
                    WantsAllCodeActionOperations = true
                };

                return await requestHandler.Handle(request);
            }
        }

        private static Range GetSelection(TextRange range)
        {
            if (range.IsEmpty)
            {
                return null;
            }

            return new Range
            {
                Start = new Point { Line = range.Start.Line, Column = range.Start.Offset },
                End = new Point { Line = range.End.Line, Column = range.End.Offset }
            };
        }
    }
}
