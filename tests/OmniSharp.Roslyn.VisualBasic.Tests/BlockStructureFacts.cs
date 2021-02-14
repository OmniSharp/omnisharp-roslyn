using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.VisualBasic.Services.Structure;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.VisualBasic.Tests
{
    public class BlockStructureFacts : AbstractSingleRequestHandlerTestFixture<BlockStructureService>
    {
        public BlockStructureFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.V2.BlockStructure;

        [Fact]
        public async Task UsesRoslynBlockStructureService()
        {
            var testFile = new TestFile("foo0.vb", @"[|Class Foo

    [|Public Sub M()
        [|If True Then
            System.Console.WriteLine()
        End If|]
    End Sub|]
End Class|]");
            var text = testFile.Content.Text;

            var lineSpans = (await GetResponseAsync(testFile)).Spans
                .Select(b => b.Range)
                .ToArray();

            var expected = testFile.Content.GetSpans()
                .Select(span => testFile.Content.GetRangeFromSpan(span).ToRange()).ToArray();

            Assert.Equal(expected, lineSpans);
        }

        [Fact]
        public async Task SupportsRegionBlocks()
        {
            var testFile = new TestFile("foo.vb", @"
[|#Region ""Code Region Here""
[|Class Foo
    [|Public Sub M()
        [|if False Then
        End If|]
    End Sub|]
End Class|]
#End Region|]");

            // TODO: Investigate why the Kind is null.
            // A starting point for investigation could be 'ConvertToWellKnownBlockType'
            //var regionSpan = Assert.Single((await GetResponseAsync(testFile)).Spans,
            //    span => span.Kind == CodeFoldingBlockKinds.Region);

            //Assert.Equal(1, regionSpan.Range.Start.Line);
            //Assert.Equal(0, regionSpan.Range.Start.Column);
            //Assert.Equal(11, regionSpan.Range.End.Line);
            //Assert.Equal(10, regionSpan.Range.End.Column);

            var lineSpans = (await GetResponseAsync(testFile)).Spans
                .Select(b => b.Range)
                .OrderBy(b => b.Start.Line)
                .ToArray();

            var expected = testFile.Content.GetSpans()
                .Select(span => testFile.Content.GetRangeFromSpan(span).ToRange()).ToArray();

            Assert.Equal(expected, lineSpans);
        }

        private Task<BlockStructureResponse> GetResponseAsync(TestFile testFile)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var request = new BlockStructureRequest
            {
                FileName = testFile.FileName,
            };

            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost, LanguageNames.VisualBasic);
            return requestHandler.Handle(request);
        }
    }
}
