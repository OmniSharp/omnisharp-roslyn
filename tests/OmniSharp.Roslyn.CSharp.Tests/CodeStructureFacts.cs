using System.Threading.Tasks;
using OmniSharp.Models.V2.CodeStructure;
using OmniSharp.Roslyn.CSharp.Services.Structure;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class CodeStructureFacts : AbstractSingleRequestHandlerTestFixture<CodeStructureService>
    {
        public CodeStructureFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.V2.CodeStructure;

        [Fact]
        public async Task AllTypes()
        {
            const string source = @"
class C { }
delegate void D();
enum E { One, Two, Three }
interface I { }
struct S { }
";

            var response = await GetCodeStructureAsync(source);
        }

        private Task<CodeStructureResponse> GetCodeStructureAsync(string source)
        {
            var testFile = new TestFile("test.cs", source);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);

            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);

            var request = new CodeStructureRequest
            {
                FileName = "test.cs"
            };

            return requestHandler.Handle(request);
        }
    }
}
