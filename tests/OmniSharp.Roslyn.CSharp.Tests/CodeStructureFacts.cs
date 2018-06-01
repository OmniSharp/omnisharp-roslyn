using System;
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

            Assert.Equal(5, response.Elements.Length);
            AssertElement(response.Elements[0], CodeElementKinds.Class, "C", "C");
            AssertElement(response.Elements[1], CodeElementKinds.Delegate, "D", "D");
            AssertElement(response.Elements[2], CodeElementKinds.Enum, "E", "E");
            AssertElement(response.Elements[3], CodeElementKinds.Interface, "I", "I");
            AssertElement(response.Elements[4], CodeElementKinds.Struct, "S", "S");
        }

        [Fact]
        public async Task AllTypesInNamespace()
        {
            const string source = @"
namespace N
{
    class C { }
    delegate void D();
    enum E { One, Two, Three }
    interface I { }
    struct S { }
}
";

            var response = await GetCodeStructureAsync(source);

            var elementN = Assert.Single(response.Elements);
            AssertElement(elementN, CodeElementKinds.Namespace, "N", "N");

            var children = elementN.Children;
            Assert.Equal(5, children.Length);
            AssertElement(children[0], CodeElementKinds.Class, "C", "N.C");
            AssertElement(children[1], CodeElementKinds.Delegate, "D", "N.D");
            AssertElement(children[2], CodeElementKinds.Enum, "E", "N.E");
            AssertElement(children[3], CodeElementKinds.Interface, "I", "N.I");
            AssertElement(children[4], CodeElementKinds.Struct, "S", "N.S");
        }

        private static void AssertElement(CodeElement element, string kind, string name, string displayName)
        {
            Assert.Equal(kind, element.Kind);
            Assert.Equal(name, element.Name);
            Assert.Equal(displayName, element.DisplayName);
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
