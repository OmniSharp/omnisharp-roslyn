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
using Roslyn.Test.Utilities;
using TestUtility;
using Xunit;
using Range = OmniSharp.Models.V2.Range;

namespace OmniSharp.Cake.Tests
{
    public abstract class CodeActionsTestBase : AbstractTestFixture
    {
        protected CodeActionsTestBase(ITestOutputHelper output)
            : base(output)
        {
        }

        protected async Task<RunCodeActionResponse> RunRefactoringAsync(string code, string refactoringName)
        {
            var refactorings = await FindRefactoringsAsync(code);
            Assert.Contains(refactoringName, refactorings.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);

            var identifier = refactorings.First(action => action.Name.Equals(refactoringName, StringComparison.OrdinalIgnoreCase)).Identifier;
            return await RunRefactoringsAsync(code, identifier);
        }

        protected async Task<IEnumerable<(string Name, string CodeActionKind)>> FindRefactoringNamesAsync(string code)
        {
            var codeActions = await FindRefactoringsAsync(code);

            return codeActions.Select(a => (a.Name, a.CodeActionKind));
        }

        protected async Task<IEnumerable<OmniSharpCodeAction>> FindRefactoringsAsync(string code)
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

        protected async Task<RunCodeActionResponse> RunRefactoringsAsync(string code, string identifier)
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

        protected static Range GetSelection(TextRange range)
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
