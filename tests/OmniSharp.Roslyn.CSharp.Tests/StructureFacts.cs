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
        public StructureFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        [Fact]
        public async Task SimpleClass()
        {
            const string source =
                @"public class Far {

                }";

            var nodes = await GetStructureAsync(source);

            Assert.Single(nodes);
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
            Assert.Single(nodes);
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

            Assert.Single(nodes);
            Assert.Equal("Far", nodes[0].Location.Text);
            Assert.Equal(SyntaxKind.InterfaceDeclaration.ToString(), nodes[0].Kind);
        }

        [Fact]
        public async Task EnsureFileNameIsSet()
        {
            const string source =
                @"public interface Far {

                }";

            const string fileName = "test.cs";

            var nodes = await GetStructureAsync(source, fileName);
            Assert.Single(nodes);
            Assert.Equal(fileName, nodes[0].Location.FileName);
        }


        private async Task<FileMemberElement[]> GetStructureAsync(string source, string fileName = "d.cs")
        {
            var testFile = new TestFile(fileName, source);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var nodes = await StructureComputer.Compute(SharedOmniSharpTestHost.Workspace.GetDocuments(testFile.FileName));
            return nodes.ToArray();
        }
    }
}
