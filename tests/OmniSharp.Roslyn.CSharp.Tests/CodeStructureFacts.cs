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

        [Fact]
        public async Task GenericTypesInNamespace()
        {
            const string source = @"
namespace N
{
    class C<T> { }
    delegate void D<T1, T2>();
    interface I<T> { }
    struct S<T> { }
}
";

            var response = await GetCodeStructureAsync(source);

            var elementN = Assert.Single(response.Elements);
            AssertElement(elementN, CodeElementKinds.Namespace, "N", "N");

            var children = elementN.Children;
            Assert.Equal(4, children.Length);
            AssertElement(children[0], CodeElementKinds.Class, "C<T>", "N.C<T>");
            AssertElement(children[1], CodeElementKinds.Delegate, "D<T1, T2>", "N.D<T1, T2>");
            AssertElement(children[2], CodeElementKinds.Interface, "I<T>", "N.I<T>");
            AssertElement(children[3], CodeElementKinds.Struct, "S<T>", "N.S<T>");
        }

        [Fact]
        public async Task NestedNamespaces()
        {
            const string source = @"
namespace N1
{
    namespace N2.N3
    {
        namespace N4 { }
    }

    namespace N5
    {
    }

    namespace N2
    {
        namespace N6 { }
    }
}
";

            var response = await GetCodeStructureAsync(source);

            var elementN1 = Assert.Single(response.Elements);
            AssertElement(elementN1, CodeElementKinds.Namespace, "N1", "N1");

            Assert.Equal(3, elementN1.Children.Length);

            var elementN3 = elementN1.Children[0];
            AssertElement(elementN3, CodeElementKinds.Namespace, "N3", "N1.N2.N3");

            var elementN4 = Assert.Single(elementN3.Children);
            AssertElement(elementN4, CodeElementKinds.Namespace, "N4", "N1.N2.N3.N4");

            var elementN5 = elementN1.Children[1];
            AssertElement(elementN5, CodeElementKinds.Namespace, "N5", "N1.N5");

            var elementN2 = elementN1.Children[2];
            AssertElement(elementN2, CodeElementKinds.Namespace, "N2", "N1.N2");

            var elementN6 = Assert.Single(elementN2.Children);
            AssertElement(elementN6, CodeElementKinds.Namespace, "N6", "N1.N2.N6");
        }

        [Fact]
        public async Task NestedTypes()
        {
            const string source = @"
class C
{
    delegate void D();
    interface I { }
    struct S
    {
        enum E { One, Two, Three }
    }
}
";

            var response = await GetCodeStructureAsync(source);

            var elementC = Assert.Single(response.Elements);
            AssertElement(elementC, CodeElementKinds.Class, "C", "C");

            Assert.Equal(3, elementC.Children.Length);

            var elementD = elementC.Children[0];
            AssertElement(elementD, CodeElementKinds.Delegate, "D", "C.D");

            var elementI = elementC.Children[1];
            AssertElement(elementI, CodeElementKinds.Interface, "I", "C.I");

            var elementS = elementC.Children[2];
            AssertElement(elementS, CodeElementKinds.Struct, "S", "C.S");

            var elementE = Assert.Single(elementS.Children);
            AssertElement(elementE, CodeElementKinds.Enum, "E", "C.S.E");
        }

        [Fact]
        public async Task ClassMembers()
        {
            const string source = @"
class C
{
    private int _f;
    private int _f1, _f2;
    private const int _c;
    public C() { }
    ~C() { }
    public void M1() { }
    public void M2(int i, ref string s, params object[] array) { }
    public static implicit operator C(int i) { return null; }
    public static C operator +(C c1, C c2) { return null; }
    public int P { get; set; }
    public event EventHandler E;
    public event EventHandler E1, E2;
    public event EventHandler E3 { add { } remove { } }
}
";

            var response = await GetCodeStructureAsync(source);

            var elementC = Assert.Single(response.Elements);
            AssertElement(elementC, CodeElementKinds.Class, "C", "C");

            var children = elementC.Children;
            Assert.Equal(15, children.Length);
            AssertElement(children[0], CodeElementKinds.Field, "_f", "_f", CodeElementAccessibilities.Private);
            AssertElement(children[1], CodeElementKinds.Field, "_f1", "_f1", CodeElementAccessibilities.Private);
            AssertElement(children[2], CodeElementKinds.Field, "_f2", "_f2", CodeElementAccessibilities.Private);
            AssertElement(children[3], CodeElementKinds.Constant, "_c", "_c", CodeElementAccessibilities.Private);
            AssertElement(children[4], CodeElementKinds.Constructor, "C", "C()", CodeElementAccessibilities.Public);
            AssertElement(children[5], CodeElementKinds.Destructor, "~C", "~C()", CodeElementAccessibilities.Protected);
            AssertElement(children[6], CodeElementKinds.Method, "M1", "M1()", CodeElementAccessibilities.Public);
            AssertElement(children[7], CodeElementKinds.Method, "M2", "M2(int i, ref string s, params object[] array)", CodeElementAccessibilities.Public);
            AssertElement(children[8], CodeElementKinds.Operator, "implicit operator C", "implicit operator C(int i)", CodeElementAccessibilities.Public);
            AssertElement(children[9], CodeElementKinds.Operator, "operator +", "operator +(C c1, C c2)", CodeElementAccessibilities.Public);
            AssertElement(children[10], CodeElementKinds.Property, "P", "P", CodeElementAccessibilities.Public);
            AssertElement(children[11], CodeElementKinds.Event, "E", "E", CodeElementAccessibilities.Public);
            AssertElement(children[12], CodeElementKinds.Event, "E1", "E1", CodeElementAccessibilities.Public);
            AssertElement(children[13], CodeElementKinds.Event, "E2", "E2", CodeElementAccessibilities.Public);
            AssertElement(children[14], CodeElementKinds.Event, "E3", "E3", CodeElementAccessibilities.Public);
        }

        private static void AssertElement(CodeElement element, string kind, string name, string displayName, string accessibility = null)
        {
            Assert.Equal(kind, element.Kind);
            Assert.Equal(name, element.Name);
            Assert.Equal(displayName, element.DisplayName);

            if (accessibility != null)
            {
                Assert.Equal(accessibility, element.Properties[CodeElementPropertyNames.Accessibility]);
            }
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
