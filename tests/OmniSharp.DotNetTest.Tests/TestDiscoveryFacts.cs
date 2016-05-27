using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.DotNetTest.Helpers;
using TestCommon;
using TestUtility;
using Xunit;

namespace OmniSharp.DotNetTest.Tests
{
    public class TestDiscoveryFacts
    {
        [Theory]
        [InlineData("BasicTestProjectSample01", "TestProgram.cs", 8, 23, true)]
        [InlineData("BasicTestProjectSample01", "TestProgram.cs", 15, 26, true)]
        [InlineData("BasicTestProjectSample01", "TestProgram.cs", 21, 28, false)]
        public async Task FoundFactsBasedTest(string projectName, string filename, int line, int column, bool found)
        {
            var sampleProject = TestsContext.Default.GetTestSample(projectName);
            var workspace = WorkspaceHelper.Create(sampleProject).FirstOrDefault();

            var docId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(Path.Combine(sampleProject, filename)).First();
            var doc = workspace.CurrentSolution.GetDocument(docId);

            var root = await doc.GetSyntaxRootAsync();
            var text = await doc.GetTextAsync();
            var semanticModel = await doc.GetSemanticModelAsync();
            var position = text.Lines.GetPosition(new LinePosition(line, column));
            var syntaxNode = root.DescendantNodes()
                                 .OfType<MethodDeclarationSyntax>()
                                 .Single(node => node.Span.Contains(position));

            var symbol = semanticModel.GetDeclaredSymbol(syntaxNode);
            var discover = new TestFeaturesDiscover();
            var features = discover.Discover(syntaxNode, await doc.GetSemanticModelAsync());

            if (found)
            {
                var feature = features.Single();
                Assert.Equal("XunitTestMethod", feature.Name);
                
                var symbolName = symbol.ToDisplayString();
                symbolName = symbolName.Substring(0, symbolName.IndexOf('('));
                Assert.Equal(symbolName, feature.Data);
            }
            else
            {
                Assert.Empty(features);
            }
        }
    }
}