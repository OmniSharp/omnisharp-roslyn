using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Roslyn.CSharp.Services.Refactoring;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FixAllFacts
    {
        private readonly ITestOutputHelper _testOutput;

        public FixAllFacts(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Fact]
        public async Task WhenFileContainsFixableIssuesWithAnalyzersEnabled_ThenFixThemAll()
        {
            using(var host = GetHost(true))
            {
                host.AddFilesToWorkspace(new TestFile("a.cs",
                @"
                    using System.IO;
                    class C {}
                "));

                var handler = host.GetRequestHandler<RunFixAllCodeActionService>(OmniSharpEndpoints.RunFixAll);

                await handler.Handle(new FixAllRequest());

                var docs = host.Workspace.CurrentSolution.Projects.SelectMany(x => x.Documents);

                foreach(var doc in docs)
                {
                    var text = await doc.GetTextAsync();
                }
            }
        }

        private OmniSharpTestHost GetHost(bool roslynAnalyzersEnabled)
        {
            return OmniSharpTestHost.Create(testOutput: _testOutput, configurationData: new Dictionary<string, string>() { { "RoslynExtensionsOptions:EnableAnalyzersSupport", roslynAnalyzersEnabled.ToString() } });
        }
    }
}
