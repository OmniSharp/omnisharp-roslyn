using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Abstractions.Models.V1.FixAll;
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
                var originalText =
                @"
                    class C{}
                ";

                var expectedText =
                @"
                    internal class C { }
                ";

                var testFilePath = CreateTestProjectWithDocument(host, originalText);

                var handler = host.GetRequestHandler<RunFixAllCodeActionService>(OmniSharpEndpoints.RunFixAll);

                var response = await handler.Handle(new RunFixAllRequest
                {
                    Scope = FixAllScope.Document,
                    FileName = testFilePath
                });

                string textAfterFix = await GetContentOfDocumentFromWorkspace(host, testFilePath);
                var changesFromOnlyDocument = response.Changes.Single().Changes.Single();

                AssertUtils.AssertIgnoringIndent(textAfterFix, expectedText);
                Assert.Equal("\ninternal class C { }\n", changesFromOnlyDocument.NewText);
                Assert.Equal(0, changesFromOnlyDocument.StartLine);
                Assert.Equal(0, changesFromOnlyDocument.StartColumn);
                Assert.Equal(2, changesFromOnlyDocument.EndLine);
                Assert.Equal(16, changesFromOnlyDocument.EndColumn);
            }
        }

        [Fact]
        public async Task WhenFixAllItemsAreDefined_ThenFixOnlyDefinedItems()
        {
            using (var host = GetHost(true))
            {
                var originalText =
                @"
                    class C{}
                ";

                var expectedText =
                @"
                    internal class C{}
                ";

                var testFilePath = CreateTestProjectWithDocument(host, originalText);

                var handler = host.GetRequestHandler<RunFixAllCodeActionService>(OmniSharpEndpoints.RunFixAll);

                var response = await handler.Handle(new RunFixAllRequest
                {
                    Scope = FixAllScope.Solution,
                    FixAllFilter = new [] { new FixAllItem("IDE0040", message: "This really doesn't matter. Works as description. Fix internal etc.") }
                });

                string textAfterFix = await GetContentOfDocumentFromWorkspace(host, testFilePath);
                var changesFromOnlyDocument = response.Changes.Single().Changes.Single();

                AssertUtils.AssertIgnoringIndent(textAfterFix, expectedText);
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

                var ide0055File = CreateTestProjectWithDocument(host, originalIde0055Text);

                var originalId0040Text =
                @"
                    class NonInternalIDEIDE0040 { }
                ";

                var ide0040File = CreateTestProjectWithDocument(host, originalId0040Text);

                var handler = host.GetRequestHandler<RunFixAllCodeActionService>(OmniSharpEndpoints.RunFixAll);

                await handler.Handle(new RunFixAllRequest
                {
                    Scope = scope,
                    FileName = ide0055File
                });

                string textAfterFixIde0055 = await GetContentOfDocumentFromWorkspace(host, ide0055File);
                string textAfterFixIde0040 = await GetContentOfDocumentFromWorkspace(host, ide0040File);

                AssertUtils.AssertIgnoringIndent(expectedIde0055TextWithFixedFormat, textAfterFixIde0055);
                AssertUtils.AssertIgnoringIndent(originalId0040Text, textAfterFixIde0040);
            }
        }

        [Fact(Skip="TODO? Something prevents this fix to execute.")]
        public async Task WhenTextContainsUnusedImports_ThenTheyCanBeAutomaticallyFixed()
        {
            using (var host = GetHost(true))
            {
                var originalText =
                @"
                    using System.IO;
                ";

                var expectedText =
                @"
                ";

                var testFilePath = CreateTestProjectWithDocument(host, originalText);

                var handler = host.GetRequestHandler<RunFixAllCodeActionService>(OmniSharpEndpoints.RunFixAll);

                var response = await handler.Handle(new RunFixAllRequest
                {
                    Scope = FixAllScope.Solution
                });

                string textAfterFix = await GetContentOfDocumentFromWorkspace(host, testFilePath);

                AssertUtils.AssertIgnoringIndent(textAfterFix, expectedText);
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

                var response = await handler.Handle(new RunFixAllRequest
                {
                    Scope = FixAllScope.Solution
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
                var ide0055File = CreateTestProjectWithDocument(host,
                    @"
                        internal class InvalidFormatIDE0055ExpectedHere{}
                    ");

                var ide0040File = CreateTestProjectWithDocument(host,
                    @"
                        class NonInternalIDEIDE0040 { }
                    ");

                var resultFromDocument = await GetFixAllTargets(host, ide0055File, scope);

                Assert.Contains(resultFromDocument.Items, x => x.Id == "IDE0055");
                Assert.DoesNotContain(resultFromDocument.Items, x => x.Id == "IDE0040");
            }
        }

        [Fact]
        public async Task WhenScoped_ThenReturnOnlyFromCorrectScope()
        {
            using (var host = GetHost(true))
            {
                host.AddFilesToWorkspace(new TestFile("a.cs",
                @"
                    class C {}
                "));

                var handler = host.GetRequestHandler<GetFixAllCodeActionService>(OmniSharpEndpoints.GetFixAll);

                var result = await handler.Handle(new GetFixAllRequest()
                {
                    Scope = FixAllScope.Solution
                });

                Assert.Contains(result.Items, x => x.Id == "IDE0055" && x.Message.Contains("Fix formatting"));
                Assert.Contains(result.Items, x => x.Id == "IDE0040" && x.Message.Contains("Accessibility modifiers required"));
            }
        }

        private OmniSharpTestHost GetHost(bool roslynAnalyzersEnabled)
        {
            return OmniSharpTestHost.Create(
                testOutput: _testOutput,
                configurationData: new Dictionary<string, string>() { { "RoslynExtensionsOptions:EnableAnalyzersSupport", roslynAnalyzersEnabled.ToString() } },
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
