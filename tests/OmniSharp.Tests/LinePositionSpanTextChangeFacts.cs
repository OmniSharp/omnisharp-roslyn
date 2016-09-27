using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using TestUtility;
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

            var textChange = new TextChange(TextSpan.FromBounds(8, 11), "\n}");
            var adjustedTextChanges = await LinePositionSpanTextChange.Convert(document, new[] { textChange });

            var adjustedTextChange = adjustedTextChanges.First();
            Assert.Equal("\r\n}", adjustedTextChange.NewText);
            Assert.Equal(0, adjustedTextChange.StartLine);
            Assert.Equal(7, adjustedTextChange.StartColumn);
            Assert.Equal(1, adjustedTextChange.EndLine);
            Assert.Equal(2, adjustedTextChange.EndColumn);
        }

        [Fact]
        public async Task ExtendsTextChangeAtEnd()
        {
            var workspace = await TestHelpers.CreateSimpleWorkspace("class {\n}");
            var document = workspace.GetDocument("dummy.cs");

            var textChange = new TextChange(TextSpan.FromBounds(5, 7), "\r\n {\r");
            var adjustedTextChanges = await LinePositionSpanTextChange.Convert(document, new[] { textChange });

            var adjustedTextChange = adjustedTextChanges.First();
            Assert.Equal("\r\n {\r\n", adjustedTextChange.NewText);
            Assert.Equal(0, adjustedTextChange.StartLine);
            Assert.Equal(5, adjustedTextChange.StartColumn);
            Assert.Equal(1, adjustedTextChange.EndLine);
            Assert.Equal(0, adjustedTextChange.EndColumn);
        }
    }
}
