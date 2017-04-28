using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Roslyn.Utilities;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Tests
{
    public class LinePositionSpanTextChangeFacts : AbstractTestFixture
    {
        public LinePositionSpanTextChangeFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task ExtendsTextChangeAtStart()
        {
            var testFile = new TestFile("dummy.cs", "class {\r\n }");
            using (var host = CreateOmniSharpHost(testFile))
            {
                var document = host.Workspace.GetDocument(testFile.FileName);
                var text = await document.GetTextAsync();

                var textChange = new TextChange(TextSpan.FromBounds(8, 11), "\n}");

                var adjustedTextChanges = TextChanges.Convert(text, textChange);

                var adjustedTextChange = adjustedTextChanges.First();
                Assert.Equal("\r\n}", adjustedTextChange.NewText);
                Assert.Equal(0, adjustedTextChange.StartLine);
                Assert.Equal(7, adjustedTextChange.StartColumn);
                Assert.Equal(1, adjustedTextChange.EndLine);
                Assert.Equal(2, adjustedTextChange.EndColumn);
            }
        }

        [Fact]
        public async Task ExtendsTextChangeAtEnd()
        {
            var testFile = new TestFile("dummy.cs", "class {\n}");
            using (var host = CreateOmniSharpHost(testFile))
            {
                var document = host.Workspace.GetDocument(testFile.FileName);
                var text = await document.GetTextAsync();

                var textChange = new TextChange(TextSpan.FromBounds(5, 7), "\r\n {\r");

                var adjustedTextChanges = TextChanges.Convert(text, textChange);

                var adjustedTextChange = adjustedTextChanges.First();
                Assert.Equal("\r\n {\r\n", adjustedTextChange.NewText);
                Assert.Equal(0, adjustedTextChange.StartLine);
                Assert.Equal(5, adjustedTextChange.StartColumn);
                Assert.Equal(1, adjustedTextChange.EndLine);
                Assert.Equal(0, adjustedTextChange.EndColumn);
            }
        }
    }
}
