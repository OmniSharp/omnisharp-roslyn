using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Cake.Services.RequestHandlers.Buffer;
using OmniSharp.Cake.Services.RequestHandlers.Refactoring.V2;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Models.V2;
using OmniSharp.Models.V2.CodeActions;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public class CodeActionsV2Facts : AbstractTestFixture
    {
        public CodeActionsV2Facts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task Can_get_code_actions_from_roslyn()
        {
            const string code = "var regex = new Reg[||]ex();";

            var refactorings = await FindRefactoringNamesAsync(code);
            Assert.Contains("using System.Text.RegularExpressions;", refactorings);
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
            Assert.Contains("Extract method", refactorings);
        }

        [Fact]
        public async Task Returns_ordered_code_actions()
        {
            const string code =
                @"public class Class1
                {
                    public void Whatever()
                    {
                        [|Regex.Match(""foo"", ""bar"");|]
                    }
                }";

            var refactorings = await FindRefactoringNamesAsync(code);
            var expected = new List<string>
            {
                "using System.Text.RegularExpressions;",
                "System.Text.RegularExpressions.Regex",
                "Extract method",
                "Extract local function",
                "Introduce local for 'Regex.Match(\"foo\", \"bar\")'",
                "Introduce parameter for 'Regex.Match(\"foo\", \"bar\")' -> and update call sites directly",
                "Introduce parameter for 'Regex.Match(\"foo\", \"bar\")' -> into extracted method to invoke at call sites",
                "Introduce parameter for 'Regex.Match(\"foo\", \"bar\")' -> into new overload",
                "Introduce parameter for all occurrences of 'Regex.Match(\"foo\", \"bar\")' -> and update call sites directly",
                "Introduce parameter for all occurrences of 'Regex.Match(\"foo\", \"bar\")' -> into extracted method to invoke at call sites",
                "Introduce parameter for all occurrences of 'Regex.Match(\"foo\", \"bar\")' -> into new overload"
            };
            Assert.Equal(expected, refactorings);
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

            var expected = new LinePositionSpanTextChange
            {
                NewText = "NewMethod();\n    }\n\n    private static void NewMethod()\n    {\n        ",
                StartLine = 4,
                StartColumn = 8,
                EndLine = 4,
                EndColumn = 8
            };

            var response = await RunRefactoringAsync(code, "Extract method");
            var modifiedFile = response.Changes.FirstOrDefault() as ModifiedFileResponse;

            Assert.Single(response.Changes);
            Assert.NotNull(modifiedFile);
            Assert.Single(modifiedFile.Changes);
            Assert.Equal(expected, modifiedFile.Changes.FirstOrDefault());
        }

        [Fact]
        public async Task Should_Not_Find_Rename_File()
        {
            const string code =
                @"public class Class[||]1
                {
                    public void Whatever()
                    {
                        Console.Write(""should be using System;"");
                    }
                }";

            var refactorings = await FindRefactoringNamesAsync(code);
            Assert.Empty(refactorings.Where(x => x.StartsWith("Rename file to")));
        }

        private async Task<RunCodeActionResponse> RunRefactoringAsync(string code, string refactoringName)
        {
            var refactorings = await FindRefactoringsAsync(code);
            Assert.Contains(refactoringName, refactorings.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);

            var identifier = refactorings.First(action => action.Name.Equals(refactoringName, StringComparison.OrdinalIgnoreCase)).Identifier;
            return await RunRefactoringsAsync(code, identifier);
        }

        private async Task<IEnumerable<string>> FindRefactoringNamesAsync(string code)
        {
            var codeActions = await FindRefactoringsAsync(code);

            return codeActions.Select(a => a.Name);
        }

        private async Task<IEnumerable<OmniSharpCodeAction>> FindRefactoringsAsync(string code)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var testFile = new TestFile(Path.Combine(testProject.Directory, "build.cake"), code);
                var requestHandler = GetGetCodeActionsHandler(host);

                var span = testFile.Content.GetSpans().Single();
                var range = testFile.Content.GetRangeFromSpan(span);

                var request = new GetCodeActionsRequest
                {
                    Line = range.Start.Line,
                    Column = range.Start.Offset,
                    FileName = testFile.FileName,
                    Buffer = testFile.Content.Code,
                    Selection = GetSelection(range),
                };

                var updateBufferRequest = new UpdateBufferRequest
                {
                    Buffer = request.Buffer,
                    Column = request.Column,
                    FileName = request.FileName,
                    Line = request.Line,
                    FromDisk = false
                };

                await GetUpdateBufferHandler(host).Handle(updateBufferRequest);

                var response = await requestHandler.Handle(request);

                return response.CodeActions;
            }
        }

        private async Task<RunCodeActionResponse> RunRefactoringsAsync(string code, string identifier)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var testFile = new TestFile(Path.Combine(testProject.Directory, "build.cake"), code);
                var requestHandler = GetRunCodeActionsHandler(host);

                var span = testFile.Content.GetSpans().Single();
                var range = testFile.Content.GetRangeFromSpan(span);

                var request = new RunCodeActionRequest
                {
                    Line = range.Start.Line,
                    Column = range.Start.Offset,
                    Selection = GetSelection(range),
                    FileName = testFile.FileName,
                    Buffer = testFile.Content.Code,
                    Identifier = identifier,
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true
                };

                var updateBufferRequest = new UpdateBufferRequest
                {
                    Buffer = request.Buffer,
                    Column = request.Column,
                    FileName = request.FileName,
                    Line = request.Line,
                    FromDisk = false
                };

                await GetUpdateBufferHandler(host).Handle(updateBufferRequest);

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

        private static GetCodeActionsHandler GetGetCodeActionsHandler(OmniSharpTestHost host)
        {
            return GetRequestHandler<GetCodeActionsHandler>(host, OmniSharpEndpoints.V2.GetCodeActions);
        }

        private static RunCodeActionsHandler GetRunCodeActionsHandler(OmniSharpTestHost host)
        {
            return GetRequestHandler<RunCodeActionsHandler>(host, OmniSharpEndpoints.V2.RunCodeAction);
        }

        private static UpdateBufferHandler GetUpdateBufferHandler(OmniSharpTestHost host)
        {
            return GetRequestHandler<UpdateBufferHandler>(host, OmniSharpEndpoints.UpdateBuffer);
        }

        private static TRequestHandler GetRequestHandler<TRequestHandler>(OmniSharpTestHost host, string endpoint) where TRequestHandler : IRequestHandler
        {
            return host.GetRequestHandler<TRequestHandler>(endpoint, Constants.LanguageNames.Cake);
        }
    }
}
