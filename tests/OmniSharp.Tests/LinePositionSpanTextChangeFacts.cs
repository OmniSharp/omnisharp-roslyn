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
            var testFile = new TestFile("dummy.cs", "class {\r\n }");
            var workspace = await TestHelpers.CreateWorkspace(testFile);
            var document = workspace.GetDocument(testFile.FileName);

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
            var testFile = new TestFile("dummy.cs", "class {\n}");
            var workspace = await TestHelpers.CreateWorkspace(testFile);
            var document = workspace.GetDocument(testFile.FileName);

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
