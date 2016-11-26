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
        [InlineData("BasicTestProjectSample01", "TestProgram.cs", 8, 23, true, "XunitTestMethod")]
        [InlineData("BasicTestProjectSample01", "TestProgram.cs", 15, 26, true, "XunitTestMethod")]
        [InlineData("BasicTestProjectSample01", "TestProgram.cs", 21, 28, false, "")]
        [InlineData("BasicTestProjectSample02", "TestProgram.cs", 8, 23, true, "NUnitTestMethod")]
        [InlineData("BasicTestProjectSample02", "TestProgram.cs", 15, 26, true, "NUnitTestMethod")]
        [InlineData("BasicTestProjectSample02", "TestProgram.cs", 21, 26, true, "NUnitTestMethod")]
        [InlineData("BasicTestProjectSample02", "TestProgram.cs", 25, 35, false, "")]
        public async Task FoundFactsBasedTest(string projectName, string filename, int line, int column, bool found, string expectedFeatureName)
        {
            var sampleProject = TestsContext.Default.GetTestSample(projectName);
            var workspace = WorkspaceHelper.Create(sampleProject).FirstOrDefault();

            var docId = workspace.CurrentSolution.GetDocumentIdsWithFilePath(Path.Combine(sampleProject, filename)).First();
            var doc = workspace.CurrentSolution.GetDocument(docId);

            var root = await doc.GetSyntaxRootAsync();
            var text = await doc.GetTextAsync();
            var semanticModel = await doc.GetSemanticModelAsync();
            var position = text.Lines.GetPosition(new LinePosition(line, column));
            var descNodes =  root.DescendantNodes();
            var methodDeclarations = descNodes.OfType<MethodDeclarationSyntax>();
            var syntaxNode = methodDeclarations
                                 .Single(node => node.Span.Contains(position));

            var symbol = semanticModel.GetDeclaredSymbol(syntaxNode);

            var discover = new TestFeaturesDiscover();
            var features = discover.Discover(syntaxNode, await doc.GetSemanticModelAsync());

            if (found)
            {
                var feature = features.Single();
                Assert.Equal(expectedFeatureName, feature.Name);

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
