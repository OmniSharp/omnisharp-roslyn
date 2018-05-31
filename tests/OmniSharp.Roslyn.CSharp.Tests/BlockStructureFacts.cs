using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Models.GotoFile;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using OmniSharp.Roslyn.CSharp.Services.Structure;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class BlockStructureFacts : AbstractSingleRequestHandlerTestFixture<BlockStructureService>
    {
        public BlockStructureFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.BlockStructure;

        [Fact]
        public async Task UsesRoslynBlockStructureService()
        {
            var testFile1 = new TestFile("foo.cs", @"class Foo
{
    void M()
    {
        if (false)
        {
        }
    }
}");
            var testFile = new TestFile("bar.cs", @"class Bar {}");

            var lineSpans = (await GetResponseAsync(testFile1)).
                    Select(l => (l.Line, l.EndLine)).ToArray();

            var expected = new [] { (0, 8), (2, 7), (4, 6) };
            Assert.Equal(expected, lineSpans);
        }


        private async Task<IEnumerable<QuickFix>> GetResponseAsync(TestFile testFile)
        {
            SharedOmniSharpTestHost.AddFilesToWorkspace(new[] { testFile });
            var request = new Request
            {
                FileName = testFile.FileName,
            };

            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);
            return await requestHandler.Handle(request);
        }
    }
}
