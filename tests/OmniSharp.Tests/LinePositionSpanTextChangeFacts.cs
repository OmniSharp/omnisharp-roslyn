using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class LinePositionSpanTextChangeFacts
    {
        [Fact]
        public async Task ExtendsTextChangeAtStart()
        {
            var workspace = await TestHelpers.CreateSimpleWorkspace("class {\r\n }");
            var document = workspace.GetDocument("dummy.cs");

            var lineChanges = await LinePositionSpanTextChange.Convert(document, new TextChange[] {
                new TextChange(TextSpan.FromBounds(8, 11), "\n}")
            });

            Assert.Equal("\r\n}", lineChanges.ElementAt(0).NewText);
            Assert.Equal(1, lineChanges.ElementAt(0).StartLine);
            Assert.Equal(8, lineChanges.ElementAt(0).StartColumn);
            Assert.Equal(2, lineChanges.ElementAt(0).EndLine);
            Assert.Equal(3, lineChanges.ElementAt(0).EndColumn);
        }

        [Fact]
        public async Task ExtendsTextChangeAtEnd()
        {
            var workspace = await TestHelpers.CreateSimpleWorkspace("class {\n}");
            var document = workspace.GetDocument("dummy.cs");

            var lineChanges = await LinePositionSpanTextChange.Convert(document, new TextChange[] {
                new TextChange(TextSpan.FromBounds(5, 7), "\r\n {\r")
            });

            Assert.Equal("\r\n {\r\n", lineChanges.ElementAt(0).NewText);
            Assert.Equal(1, lineChanges.ElementAt(0).StartLine);
            Assert.Equal(6, lineChanges.ElementAt(0).StartColumn);
            Assert.Equal(2, lineChanges.ElementAt(0).EndLine);
            Assert.Equal(1, lineChanges.ElementAt(0).EndColumn);
        }
    }
}
