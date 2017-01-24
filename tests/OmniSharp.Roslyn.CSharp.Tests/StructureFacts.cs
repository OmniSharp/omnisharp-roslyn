using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Tests
{
    public class StructureFacts : AbstractTestFixture
    {
        public StructureFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task SimpleClass()
        {
            const string source =
                @"public class Far {

                }";

            var testFile = new TestFile("d.cs", source);
            var workspace = await CreateWorkspaceAsync(testFile);

            var nodes = await StructureComputer.Compute(workspace.GetDocuments(testFile.FileName));
            Assert.Equal(1, nodes.Count());
            Assert.Equal("Far", nodes.First().Location.Text);
            Assert.Equal(SyntaxKind.ClassDeclaration.ToString(), nodes.First().Kind);
        }

        [Fact]
        public async Task ClassWithMembers()
        {
            var source =
                @"public class Far {
                    private bool _b;
                    private bool B { get; set; }
                    public void M() { }
                    public event Action E;
                }";

            var testFile = new TestFile("d.cs", source);
            var workspace = await CreateWorkspaceAsync(testFile);

            var nodes = await StructureComputer.Compute(workspace.GetDocuments(testFile.FileName));
            Assert.Equal(1, nodes.Count());
            Assert.Equal("Far", nodes.First().Location.Text);
            Assert.Equal(SyntaxKind.ClassDeclaration.ToString(), nodes.First().Kind);

            // children
            var children = nodes.First().ChildNodes;
            Assert.Equal(4, children.Count());
            Assert.Equal("_b", children.ElementAt(0).Location.Text);
            Assert.Equal("B", children.ElementAt(1).Location.Text);
            Assert.Equal("M", children.ElementAt(2).Location.Text);
            Assert.Equal("E", children.ElementAt(3).Location.Text);
        }

        [Fact]
        public async Task SimpleInterface()
        {
            var source =
                @"public interface Far {

                }";

            var testFile = new TestFile("d.cs", source);
            var workspace = await CreateWorkspaceAsync(testFile);

            var nodes = await StructureComputer.Compute(workspace.GetDocuments(testFile.FileName));
            Assert.Equal(1, nodes.Count());
            Assert.Equal("Far", nodes.First().Location.Text);
            Assert.Equal(SyntaxKind.InterfaceDeclaration.ToString(), nodes.First().Kind);
        }
    }
}
