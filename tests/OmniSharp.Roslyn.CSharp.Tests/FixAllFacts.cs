using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
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

                host.AddFilesToWorkspace(new TestFile("a.cs", originalText));
                await host.RequestCodeCheckAsync("a.cs");

                var handler = host.GetRequestHandler<RunFixAllCodeActionService>(OmniSharpEndpoints.RunFixAll);

                await handler.Handle(new RunFixAllRequest
                {
                    Scope = FixAllScope.Solution
                });

                var docAfterUpdate = host.Workspace.CurrentSolution.Projects.SelectMany(x => x.Documents).First(x => x.FilePath.EndsWith("a.cs"));
                var text = await docAfterUpdate.GetTextAsync();

                AssertUtils.AssertIgnoringIndent(originalText, expectedText);
            }
        }

        [Fact]
        public Task WhenFixAllIsScopedToDocument_ThenOnlyFixDocumentInsteadOfEverything()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task WhenAvailableFixAllActionsAreRequested_ThenReturnThemAtResponse()
        {
            using (var host = GetHost(true))
            {
                var ide0055Project = host.AddFilesToWorkspace(new TestFile("InvalidFormatIDE0055ExpectedHere.cs",
                @"
                    internal class InvalidFormatIDE0055ExpectedHere{}
                ")).First();

                var ide0040Project = host.AddFilesToWorkspace(new TestFile("NonInternalIDEIDE0040.cs",
                @"
                    class NonInternalIDEIDE0040 { }
                ")).First();

                var resultFromDocument = await GetFixAllTargets(host, ide0055Project, FixAllScope.Document);
                var resultFromProject = await GetFixAllTargets(host, ide0055Project, FixAllScope.Project);
                var resultFromSolution = await GetFixAllTargets(host, ide0055Project, FixAllScope.Solution);

                Assert.Contains(resultFromDocument.Items, x => x.Id == "IDE0055");
                Assert.DoesNotContain(resultFromDocument.Items, x => x.Id == "IDE0040");

                Assert.Contains(resultFromProject.Items, x => x.Id == "IDE0055");
                Assert.DoesNotContain(resultFromProject.Items, x => x.Id == "IDE0040");

                Assert.Contains(resultFromSolution.Items, x => x.Id == "IDE0055");
                Assert.Contains(resultFromSolution.Items, x => x.Id == "IDE0040");
            }
        }

        private static async Task<GetFixAllResponse> GetFixAllTargets(OmniSharpTestHost host, ProjectId projectId, FixAllScope scope)
        {
            var handler = host.GetRequestHandler<GetFixAllCodeActionService>(OmniSharpEndpoints.GetFixAll);

            return await handler.Handle(new GetFixAllRequest()
            {
                FileName = host.Workspace.CurrentSolution.GetProject(projectId).Documents.Single().FilePath,
                Scope = scope
            });
        }

        [Fact]
        public async Task WhenScoped_ThenReturnOnlyFromCorrectScope()
        {
            using (var host = GetHost(true))
            {
                host.AddFilesToWorkspace(new TestFile("a.cs",
                @"
                    using System.IO;
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
    }
}
