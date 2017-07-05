using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.V2;
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

        [Fact]
        public async Task Can_get_code_actions_from_roslyn()
        {
            const string code =
                @"public class Class1
                    {
                        public void Whatever()
                        {
                            Gu[||]id.NewGuid();
                        }
                    }";

            var refactorings = await FindRefactoringNamesAsync(code);
            Assert.Contains("using System;", refactorings);
        }

        [Fact]
        public async Task Can_get_code_actions_from_external_source()
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
                { "RoslynExtensionsOptions:LocationPaths:0", TestAssets.Instance.TestBinariesFolder }
            };
            var refactorings = await FindRefactoringsAsync(code, configuration);

            Assert.NotEmpty(refactorings);
            Assert.Contains("Add ConfigureAwait(false)", refactorings.Select(x => x.Name));
        }

        [Fact]
        public async Task Can_remove_unnecessary_usings()
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

            var response = await RunRefactoringAsync(code, "Remove Unnecessary Usings");
            AssertIgnoringIndent(expected, response.Changes.First().Buffer);
        }

        [Fact]
        public async Task Can_get_ranged_code_action()
        {
            const string code =
                @"public class Class1
                {
                    public void Whatever()
                    {
                        [|Console.Write(""should be using System;"");|]
                    }
                }";

            var refactorings = await FindRefactoringNamesAsync(code);
            Assert.Contains("Extract Method", refactorings);
        }

        [Fact]
        public async Task Can_extract_method()
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

            var response = await RunRefactoringAsync(code, "Extract Method");
            AssertIgnoringIndent(expected, response.Changes.First().Buffer);
        }

        private static void AssertIgnoringIndent(string expected, string actual)
        {
            Assert.Equal(TrimLines(expected), TrimLines(actual), false, true, true);
        }

        private static string TrimLines(string source)
        {
            return string.Join("\n", source.Split('\n').Select(s => s.Trim()));
        }

        private async Task<RunCodeActionResponse> RunRefactoringAsync(string code, string refactoringName, bool wantsChanges = false)
        {
            var refactorings = await FindRefactoringsAsync(code);
            Assert.Contains(refactoringName, refactorings.Select(a => a.Name));

            var identifier = refactorings.First(action => action.Name.Equals(refactoringName)).Identifier;
            return await RunRefactoringsAsync(code, identifier, wantsChanges);
        }

        private async Task<IEnumerable<string>> FindRefactoringNamesAsync(string code)
        {
            var codeActions = await FindRefactoringsAsync(code);

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
                    Selection = GetSelection(range)
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
                    WantsTextChanges = wantsChanges
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
