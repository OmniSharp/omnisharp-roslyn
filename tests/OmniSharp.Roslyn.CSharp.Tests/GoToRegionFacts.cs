using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.GotoRegion;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class GoToRegionFacts : AbstractSingleRequestHandlerTestFixture<GotoRegionService>
    {
        public GoToRegionFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.GotoRegion;

        [Fact]
        public async Task CanFindRegionsInFileWithRegions()
        {
            const string source = @"
                public class Foo
                {
                      #region A
                      public string A$$Property {get; set;}
                      #endregion

                      #region B
                      public string BProperty {get; set;}
                      #endregion
                }";

            var regions = await GetRegionsAsync(source);

            Assert.Equal(4, regions.Length);
            Assert.Equal("#region A", regions[0].Text);
            Assert.Equal(3, regions[0].Line);
            Assert.Equal("#endregion", regions[1].Text);
            Assert.Equal(5, regions[1].Line);
            Assert.Equal("#region B", regions[2].Text);
            Assert.Equal(7, regions[2].Line);
            Assert.Equal("#endregion", regions[3].Text);
            Assert.Equal(9, regions[3].Line);
        }

        [Fact]
        public async Task DoesNotFindRegionsInFileWithoutRegions()
        {
            const string source = @"public class Fo$$o{}";

            var regions = await GetRegionsAsync(source);
            Assert.Equal(0, regions.Length);
        }

        private async Task<QuickFix[]> GetRegionsAsync(string source)
        {
            var testFile = new TestFile("dummy.cs", source);
            var point = testFile.Content.GetPointFromPosition();

            using (var host = CreateOmniSharpHost(testFile))
            {
                var requestHandler = GetRequestHandler(host);

                var request = new GotoRegionRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = testFile.FileName,
                    Buffer = testFile.Content.Code
                };

                var response = await requestHandler.Handle(request);

                return response.QuickFixes.ToArray();
            }
        }
    }
}
