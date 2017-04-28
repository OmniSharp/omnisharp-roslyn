using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.Models.MembersTree;
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

            var nodes = await GetStructureAsync(source);

            Assert.Equal(1, nodes.Length);
            Assert.Equal("Far", nodes[0].Location.Text);
            Assert.Equal(SyntaxKind.ClassDeclaration.ToString(), nodes.First().Kind);
        }

        [Fact]
        public async Task ClassWithMembers()
        {
            const string source =
                @"public class Far {
                    private bool _b;
                    private bool B { get; set; }
                    public void M() { }
                    public event Action E;
                }";


            var nodes = await GetStructureAsync(source);
            Assert.Equal(1, nodes.Length);
            Assert.Equal("Far", nodes[0].Location.Text);
            Assert.Equal(SyntaxKind.ClassDeclaration.ToString(), nodes[0].Kind);

            // children
            var children = nodes[0].ChildNodes.ToArray();
            Assert.Equal(4, children.Length);
            Assert.Equal("_b", children[0].Location.Text);
            Assert.Equal("B", children[1].Location.Text);
            Assert.Equal("M", children[2].Location.Text);
            Assert.Equal("E", children[3].Location.Text);
        }

        [Fact]
        public async Task SimpleInterface()
        {
            const string source =
                @"public interface Far {

                }";

            var nodes = await GetStructureAsync(source);

            Assert.Equal(1, nodes.Length);
            Assert.Equal("Far", nodes[0].Location.Text);
            Assert.Equal(SyntaxKind.InterfaceDeclaration.ToString(), nodes[0].Kind);
        }

        private async Task<FileMemberElement[]> GetStructureAsync(string source)
        {
            var testFile = new TestFile("d.cs", source);
            using (var host = CreateOmniSharpHost(testFile))
            {
                var nodes = await StructureComputer.Compute(host.Workspace.GetDocuments(testFile.FileName));
                return nodes.ToArray();
            }
        }
    }
}
