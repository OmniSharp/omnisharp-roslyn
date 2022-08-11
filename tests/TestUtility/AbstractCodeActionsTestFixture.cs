using System;
using System.Collections.Generic;
using System.Composition.Hosting.Core;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp;
using OmniSharp.Models.V2;
using OmniSharp.Models.V2.CodeActions;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;
using TestUtility.Logging;
using Xunit;
using Xunit.Abstractions;
using Range = OmniSharp.Models.V2.Range;

namespace TestUtility
{
    public abstract class AbstractCodeActionsTestFixture
    {
        private readonly string _fileTypeExtension;

        protected ITestOutputHelper TestOutput { get; }

        private string BufferPath => $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}somepath{Path.DirectorySeparatorChar}buffer{_fileTypeExtension}";

        protected AbstractCodeActionsTestFixture(ITestOutputHelper output, string fileTypeExtension = ".cs")
        {
            TestOutput = output;

            _fileTypeExtension = fileTypeExtension;
        }

        protected void AssertIgnoringIndent(string expected, string actual)
        {
            Assert.Equal(TrimLines(expected), TrimLines(actual), false, true, true);
        }

        private static string TrimLines(string source)
        {
            return string.Join("", source.Split('\n').Select(s => s.Trim()));
        }

        protected OmniSharpTestHost CreateOmniSharpHost(TestFile[] testFiles, IConfiguration configurationData = null)
        {
            var host = OmniSharpTestHost.Create(path: null, testOutput: this.TestOutput, configurationData: configurationData);

            if (testFiles.Length > 0)
            {
                host.AddFilesToWorkspace(testFiles);
            }

            return host;
        }

        protected async Task<RunCodeActionResponse> RunRefactoringAsync(string code, string refactoringName, bool wantsChanges = false, bool isAnalyzersEnabled = true)
        {
            var refactorings = await FindRefactoringsAsync(code, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(isAnalyzersEnabled));
            Assert.Contains(refactoringName, refactorings.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);

            var identifier = refactorings.First(action => action.Name.Equals(refactoringName, StringComparison.OrdinalIgnoreCase)).Identifier;
            return await RunRefactoringsAsync(code, identifier, wantsChanges);
        }

        protected async Task<IEnumerable<(string Name, string CodeActionKind)>> FindRefactoringNamesAsync(string code, bool isAnalyzersEnabled = true, bool analyzeOpenDocumentsOnly = false)
        {
            var codeActions = await FindRefactoringsAsync(code, TestHelpers.GetConfigurationDataWithAnalyzerConfig(isAnalyzersEnabled, analyzeOpenDocumentsOnly: analyzeOpenDocumentsOnly));

            return codeActions.Select(a => (a.Name, a.CodeActionKind));
        }

        protected async Task<IEnumerable<OmniSharpCodeAction>> FindRefactoringsAsync(string code, IConfiguration configurationData = null)
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

            using (var host = CreateOmniSharpHost(new [] { testFile }))
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
