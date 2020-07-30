using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Abstractions.Models.V1.FixAll;
using OmniSharp.Models;
using OmniSharp.Models.Events;
using OmniSharp.Roslyn.CSharp.Services.Refactoring;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FixAllFacts
    {
        private readonly ITestOutputHelper _testOutput;
        private readonly TestEventEmitter<ProjectDiagnosticStatusMessage> _analysisEventListener;

        public FixAllFacts(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
            _analysisEventListener = new TestEventEmitter<ProjectDiagnosticStatusMessage>();
        }

        [Fact]
        public async Task WhenFileContainsFixableIssuesWithAnalyzersEnabled_ThenFixThemAll()
        {
            using (var host = GetHost(true))
            {
                var originalText = @"class C {}";

                var expectedText = @"internal class C { }";

                var testFilePath = CreateTestProjectWithDocument(host, originalText);

                string textBeforeFix = await GetContentOfDocumentFromWorkspace(host, testFilePath);

                var handler = host.GetRequestHandler<RunFixAllCodeActionService>(OmniSharpEndpoints.RunFixAll);

                var response = await handler.Handle(new RunFixAllRequest
                {
                    Scope = FixAllScope.Document,
                    FileName = testFilePath,
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true
                });

                string textAfterFix = await GetContentOfDocumentFromWorkspace(host, testFilePath);
                AssertUtils.AssertIgnoringIndent(textAfterFix, expectedText);

                var internalClassChange = response.Changes.OfType<ModifiedFileResponse>().Single().Changes.Single(x => x.NewText == "internal ");

                Assert.Equal(0, internalClassChange.StartLine);
                Assert.Equal(0, internalClassChange.StartColumn);
                Assert.Equal(0, internalClassChange.EndLine);
                Assert.Equal(0, internalClassChange.EndColumn);

                var formatFix = response.Changes.OfType<ModifiedFileResponse>().Single().Changes.Single(x => x.NewText == " ");

                Assert.Equal(0, formatFix.StartLine);
                Assert.Equal(9, formatFix.StartColumn);
                Assert.Equal(0, formatFix.EndLine);
                Assert.Equal(9, formatFix.EndColumn);
            }
        }

        [Fact]
        public async Task WhenFixAllItemsAreDefinedByFilter_ThenFixOnlyFilteredItems()
        {
            using (var host = GetHost(true))
            {
                var originalText =
                @"
                    class C{}
                ";

                // If filtering isn't set, this should also add 'internal' etc which
                // should not appear now as result.
                var expectedText =
                @"
                    class C { }
                ";

                var testFilePath = CreateTestProjectWithDocument(host, originalText);

                var handler = host.GetRequestHandler<RunFixAllCodeActionService>(OmniSharpEndpoints.RunFixAll);

                await handler.Handle(new RunFixAllRequest
                {
                    Scope = FixAllScope.Document,
                    FileName = testFilePath,
                    FixAllFilter = new[] { new FixAllItem("IDE0055", "Fix formatting") },
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true
                });

                string textAfterFix = await GetContentOfDocumentFromWorkspace(host, testFilePath);

                AssertUtils.AssertIgnoringIndent(expectedText, textAfterFix);
            }
        }

        [Theory]
        [InlineData(FixAllScope.Document)]
        [InlineData(FixAllScope.Project)]
        public async Task WhenFixAllIsScopedToDocumentAndProject_ThenOnlyFixInScopeInsteadOfEverything(FixAllScope scope)
        {
            using (var host = GetHost(true))
            {
                var originalIde0055Text =
                @"
                    internal class InvalidFormatIDE0055ExpectedHere{}
                ";

                var expectedIde0055TextWithFixedFormat =
                @"
                    internal class InvalidFormatIDE0055ExpectedHere { }
                ";

                var fileInScope = CreateTestProjectWithDocument(host, originalIde0055Text);

                var fileNotInScope = CreateTestProjectWithDocument(host, originalIde0055Text);

                var handler = host.GetRequestHandler<RunFixAllCodeActionService>(OmniSharpEndpoints.RunFixAll);

                await handler.Handle(new RunFixAllRequest
                {
                    Scope = scope,
                    FileName = fileInScope,
                    FixAllFilter = new[] { new FixAllItem("IDE0055", "Fix formatting") },
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true
                });

                string textAfterFixInScope = await GetContentOfDocumentFromWorkspace(host, fileInScope);
                string textAfterNotInScope = await GetContentOfDocumentFromWorkspace(host, fileNotInScope);

                AssertUtils.AssertIgnoringIndent(expectedIde0055TextWithFixedFormat, textAfterFixInScope);
                AssertUtils.AssertIgnoringIndent(originalIde0055Text, textAfterNotInScope);
            }
        }

        [Fact(Skip = @"Fails on windows only inside roslyn
System.ArgumentOutOfRangeException
Specified argument was out of the range of valid values.
Parameter name: start
...
")]
        // This is specifically tested because has custom mapping logic in it.
        public async Task WhenTextContainsUnusedImports_ThenTheyCanBeAutomaticallyFixed()
        {
            using (var host = GetHost(true))
            {
                var originalText =
                @"
                    using System.IO;
                ";

                var expectedText = @"";

                var testFilePath = CreateTestProjectWithDocument(host, originalText);

                var handler = host.GetRequestHandler<RunFixAllCodeActionService>(OmniSharpEndpoints.RunFixAll);

                await handler.Handle(new RunFixAllRequest
                {
                    Scope = FixAllScope.Document,
                    FileName = testFilePath,
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true
                });

                string textAfterFix = await GetContentOfDocumentFromWorkspace(host, testFilePath);

                AssertUtils.AssertIgnoringIndent(expectedText, textAfterFix);
            }
        }

        [Fact()]
        public async Task WhenIssueThatCannotBeAutomaticallyFixedIsAvailable_ThenDontTryToFixIt()
        {
            using (var host = GetHost(true))
            {
                var originalText =
                @"
                    invalidSyntaxThatCannotBeFixedHere
                ";

                var expectedText =
                @"
                    invalidSyntaxThatCannotBeFixedHere
                ";

                var testFilePath = CreateTestProjectWithDocument(host, originalText);

                var handler = host.GetRequestHandler<RunFixAllCodeActionService>(OmniSharpEndpoints.RunFixAll);

                await handler.Handle(new RunFixAllRequest
                {
                    Scope = FixAllScope.Document,
                    FileName = testFilePath,
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true
                });

                string textAfterFix = await GetContentOfDocumentFromWorkspace(host, testFilePath);

                AssertUtils.AssertIgnoringIndent(textAfterFix, expectedText);
            }
        }

        [Fact]
        public async Task WhenAvailableFixAllActionsAreRequested_ThenReturnThemAtResponse()
        {
            using (var host = GetHost(true))
            {
                var ide0055File = CreateTestProjectWithDocument(host,
                    @"
                        internal class InvalidFormatIDE0055ExpectedHere{}
                    ");

                var resultFromDocument = await GetFixAllTargets(host, ide0055File, FixAllScope.Document);

                Assert.Contains(resultFromDocument.Items, x => x.Id == "IDE0055");
                Assert.DoesNotContain(resultFromDocument.Items, x => x.Id == "IDE0040");
            }
        }

        [Theory]
        [InlineData(FixAllScope.Document)]
        [InlineData(FixAllScope.Project)]
        public async Task WhenGetActionIsScoped_ThenReturnOnlyItemsFromCorrectScope(FixAllScope scope)
        {
            using (var host = GetHost(true))
            {
                var inScopeFile = CreateTestProjectWithDocument(host,
                @"
                    internal class InvalidFormatIDE0055ExpectedHere{}
                ");

                var notInScopeFile = CreateTestProjectWithDocument(host,
                @"
                    class NonInternalIDEIDE0040 { }
                ");

                var resultFromDocument = await GetFixAllTargets(host, inScopeFile, scope);

                Assert.Contains(resultFromDocument.Items, x => x.Id == "IDE0055");
                Assert.DoesNotContain(resultFromDocument.Items, x => x.Id == "IDE0040");
            }
        }

        [Fact]
        // Currently fix every problem in project or solution scope is not supported, only documents.
        public async Task WhenProjectOrSolutionIsScopedWithFixEverything_ThenThrowNotImplementedException()
        {
            using (var host = GetHost(true))
            {
                var textWithFormattingProblem =
                @"
                    class C{}
                ";

                var testFilePath = CreateTestProjectWithDocument(host, textWithFormattingProblem);

                var handler = host.GetRequestHandler<RunFixAllCodeActionService>(OmniSharpEndpoints.RunFixAll);

                await handler.Handle(new RunFixAllRequest
                {
                    Scope = FixAllScope.Document,
                    FileName = testFilePath,
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true,
                    FixAllFilter = null // This means: try fix everything.
                });

                await Assert.ThrowsAsync<NotImplementedException>(async () => await handler.Handle(new RunFixAllRequest
                {
                    Scope = FixAllScope.Project,
                    FileName = testFilePath,
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true,
                    FixAllFilter = null // This means: try fix everything.
                }));

                await Assert.ThrowsAsync<NotImplementedException>(async () => await handler.Handle(new RunFixAllRequest
                {
                    Scope = FixAllScope.Solution,
                    FileName = testFilePath,
                    WantsTextChanges = true,
                    WantsAllCodeActionOperations = true,
                    FixAllFilter = null
                }));
            }
        }

        private OmniSharpTestHost GetHost(bool roslynAnalyzersEnabled)
        {
            return OmniSharpTestHost.Create(
                testOutput: _testOutput,
                configurationData: new Dictionary<string, string>() { { "RoslynExtensionsOptions:EnableAnalyzersSupport", roslynAnalyzersEnabled.ToString() } }.ToConfiguration(),
                eventEmitter: _analysisEventListener
            );
        }

        private static string CreateTestProjectWithDocument(OmniSharpTestHost host, string content)
        {
            var fileName = $"{Guid.NewGuid()}.cs";
            var projectId = host.AddFilesToWorkspace(new TestFile(fileName, content)).First();
            return host.Workspace.CurrentSolution.GetProject(projectId).Documents.Single().FilePath;
        }

        private static async Task<GetFixAllResponse> GetFixAllTargets(OmniSharpTestHost host, string fileName, FixAllScope scope)
        {
            var handler = host.GetRequestHandler<GetFixAllCodeActionService>(OmniSharpEndpoints.GetFixAll);

            return await handler.Handle(new GetFixAllRequest()
            {
                FileName = fileName,
                Scope = scope
            });
        }

        private static async Task<string> GetContentOfDocumentFromWorkspace(OmniSharpTestHost host, string testFilePath)
        {
            var docAfterUpdate = host.Workspace.CurrentSolution.Projects.SelectMany(x => x.Documents).First(x => x.FilePath == testFilePath);
            return (await docAfterUpdate.GetTextAsync()).ToString();
        }
    }
}
