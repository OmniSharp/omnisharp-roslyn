using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models.V2.CodeStructure;
using OmniSharp.Roslyn.CSharp.Services.Structure;
using OmniSharp.Services;
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
delegate void D(int i, ref string s);
enum E { One, Two, Three }
interface I { }
struct S { }
";

            var response = await GetCodeStructureAsync(source);

            Assert.Equal(5, response.Elements.Count);
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
    delegate void D(int i, ref string s);
    enum E { One, Two, Three }
    interface I { }
    struct S { }
}
";

            var response = await GetCodeStructureAsync(source);

            var elementN = Assert.Single(response.Elements);
            AssertElement(elementN, CodeElementKinds.Namespace, "N", "N");

            var children = elementN.Children;
            Assert.Equal(5, children.Count);
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
    delegate void D<T1, T2>(int i, ref string s);
    interface I<T> { }
    struct S<T> { }
}
";

            var response = await GetCodeStructureAsync(source);

            var elementN = Assert.Single(response.Elements);
            AssertElement(elementN, CodeElementKinds.Namespace, "N", "N");

            var children = elementN.Children;
            Assert.Equal(4, children.Count);
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

            Assert.Equal(3, elementN1.Children.Count);

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
    delegate void D(int i, ref string s);
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

            Assert.Equal(3, elementC.Children.Count);

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
    internal int this[int index] => 42;
}
";

            var response = await GetCodeStructureAsync(source);

            var elementC = Assert.Single(response.Elements);
            AssertElement(elementC, CodeElementKinds.Class, "C", "C", @static: false);

            var children = elementC.Children;
            Assert.Equal(16, children.Count);
            AssertElement(children[0], CodeElementKinds.Field, "_f", "_f", CodeElementAccessibilities.Private, @static: false);
            AssertElement(children[1], CodeElementKinds.Field, "_f1", "_f1", CodeElementAccessibilities.Private, @static: false);
            AssertElement(children[2], CodeElementKinds.Field, "_f2", "_f2", CodeElementAccessibilities.Private, @static: false);
            AssertElement(children[3], CodeElementKinds.Constant, "_c", "_c", CodeElementAccessibilities.Private, @static: true);
            AssertElement(children[4], CodeElementKinds.Constructor, "C", "C()", CodeElementAccessibilities.Public, @static: false);
            AssertElement(children[5], CodeElementKinds.Destructor, "~C", "~C()", CodeElementAccessibilities.Protected, @static: false);
            AssertElement(children[6], CodeElementKinds.Method, "M1", "M1()", CodeElementAccessibilities.Public, @static: false);
            AssertElement(children[7], CodeElementKinds.Method, "M2", "M2(int i, ref string s, params object[] array)", CodeElementAccessibilities.Public, @static: false);
            AssertElement(children[8], CodeElementKinds.Operator, "implicit operator C", "implicit operator C(int i)", CodeElementAccessibilities.Public, @static: true);
            AssertElement(children[9], CodeElementKinds.Operator, "operator +", "operator +(C c1, C c2)", CodeElementAccessibilities.Public, @static: true);
            AssertElement(children[10], CodeElementKinds.Property, "P", "P", CodeElementAccessibilities.Public, @static: false);
            AssertElement(children[11], CodeElementKinds.Event, "E", "E", CodeElementAccessibilities.Public, @static: false);
            AssertElement(children[12], CodeElementKinds.Event, "E1", "E1", CodeElementAccessibilities.Public, @static: false);
            AssertElement(children[13], CodeElementKinds.Event, "E2", "E2", CodeElementAccessibilities.Public, @static: false);
            AssertElement(children[14], CodeElementKinds.Event, "E3", "E3", CodeElementAccessibilities.Public, @static: false);
            AssertElement(children[15], CodeElementKinds.Indexer, "this", "this[int index]", CodeElementAccessibilities.Internal, @static: false);
        }

        [Fact]
        public async Task StaticClassAndMembers()
        {
            const string source = @"
static class C
{
    private static int _f;
    private static int _f1, _f2;
    private const int _c;
    static C() { }
    public static void M1() { }
    public static void M2(int i, ref string s, params object[] array) { }
    public static int P { get; set; }
    public static event EventHandler E;
    public static event EventHandler E1, E2;
    public static event EventHandler E3 { add { } remove { } }
}
";

            var response = await GetCodeStructureAsync(source);

            var elementC = Assert.Single(response.Elements);
            AssertElement(elementC, CodeElementKinds.Class, "C", "C");

            var children = elementC.Children;
            Assert.Equal(12, children.Count);
            AssertElement(children[0], CodeElementKinds.Field, "_f", "_f", CodeElementAccessibilities.Private, @static: true);
            AssertElement(children[1], CodeElementKinds.Field, "_f1", "_f1", CodeElementAccessibilities.Private, @static: true);
            AssertElement(children[2], CodeElementKinds.Field, "_f2", "_f2", CodeElementAccessibilities.Private, @static: true);
            AssertElement(children[3], CodeElementKinds.Constant, "_c", "_c", CodeElementAccessibilities.Private, @static: true);
            AssertElement(children[4], CodeElementKinds.Constructor, "C", "C()", CodeElementAccessibilities.Private, @static: true);
            AssertElement(children[5], CodeElementKinds.Method, "M1", "M1()", CodeElementAccessibilities.Public, @static: true);
            AssertElement(children[6], CodeElementKinds.Method, "M2", "M2(int i, ref string s, params object[] array)", CodeElementAccessibilities.Public, @static: true);
            AssertElement(children[7], CodeElementKinds.Property, "P", "P", CodeElementAccessibilities.Public, @static: true);
            AssertElement(children[8], CodeElementKinds.Event, "E", "E", CodeElementAccessibilities.Public, @static: true);
            AssertElement(children[9], CodeElementKinds.Event, "E1", "E1", CodeElementAccessibilities.Public, @static: true);
            AssertElement(children[10], CodeElementKinds.Event, "E2", "E2", CodeElementAccessibilities.Public, @static: true);
            AssertElement(children[11], CodeElementKinds.Event, "E3", "E3", CodeElementAccessibilities.Public, @static: true);
        }

        [Fact]
        public async Task HasAttributes()
        {
            const string source = @"
[Hello]
class C
{
    [World]
    void M() { }
}
";

            var response = await GetCodeStructureAsync(source);

            var elementC = Assert.Single(response.Elements);
            Assert.Contains(elementC.Ranges, kvp => kvp.Key == CodeElementRangeNames.Full);
            Assert.Contains(elementC.Ranges, kvp => kvp.Key == CodeElementRangeNames.Name);
            Assert.Contains(elementC.Ranges, kvp => kvp.Key == CodeElementRangeNames.Attributes);

            var elementM = Assert.Single(elementC.Children);
            Assert.Contains(elementM.Ranges, kvp => kvp.Key == CodeElementRangeNames.Full);
            Assert.Contains(elementM.Ranges, kvp => kvp.Key == CodeElementRangeNames.Name);
            Assert.Contains(elementM.Ranges, kvp => kvp.Key == CodeElementRangeNames.Attributes);
        }

        [Fact]
        public async Task HasNoAttributes()
        {
            const string source = @"
class C
{
    void M() { }
}
";

            var response = await GetCodeStructureAsync(source);

            var elementC = Assert.Single(response.Elements);
            Assert.Contains(elementC.Ranges, kvp => kvp.Key == CodeElementRangeNames.Full);
            Assert.Contains(elementC.Ranges, kvp => kvp.Key == CodeElementRangeNames.Name);
            Assert.DoesNotContain(elementC.Ranges, kvp => kvp.Key == CodeElementRangeNames.Attributes);

            var elementM = Assert.Single(elementC.Children);
            Assert.Contains(elementM.Ranges, kvp => kvp.Key == CodeElementRangeNames.Full);
            Assert.Contains(elementM.Ranges, kvp => kvp.Key == CodeElementRangeNames.Name);
            Assert.DoesNotContain(elementM.Ranges, kvp => kvp.Key == CodeElementRangeNames.Attributes);
        }

        private class NameLengthPropertyProvider : ICodeElementPropertyProvider
        {
            public IEnumerable<(string name, object value)> ProvideProperties(ISymbol symbol)
            {
                yield return ("namelength", symbol.Name.Length);
            }
        }

        [Fact]
        public async Task TestPropertyProvider()
        {
            const string source = @"
class ClassName
{
    void MethodName() { }
}
";
            var propertyProvider = new NameLengthPropertyProvider();
            var export = MefValueProvider.From<ICodeElementPropertyProvider>(propertyProvider);

            using (var host = CreateOmniSharpHost(additionalExports: new[] { export }))
            {
                var response = await GetCodeStructureAsync(source, host);

                var elementC = Assert.Single(response.Elements);
                Assert.Contains(elementC.Properties, kvp => kvp.Key == "namelength" && (int)kvp.Value == 9);

                var elementM = Assert.Single(elementC.Children);
                Assert.Contains(elementM.Properties, kvp => kvp.Key == "namelength" && (int)kvp.Value == 10);
            }
        }

        [Fact]
        public async Task TestTypeNameRanges()
        {
            const string source = @"
class {|nameC:C|} { }
delegate void {|nameD:D|}(int i, ref string s);
enum {|nameE:E|} { One, Two, Three }
interface {|nameI:I|} { }
struct {|nameS:S|} { }
";

            var testFile = new TestFile("test.cs", source);

            var response = await GetCodeStructureAsync(testFile);

            AssertRange(response.Elements[0], testFile.Content, "nameC", "name");
            AssertRange(response.Elements[1], testFile.Content, "nameD", "name");
            AssertRange(response.Elements[2], testFile.Content, "nameE", "name");
            AssertRange(response.Elements[3], testFile.Content, "nameI", "name");
            AssertRange(response.Elements[4], testFile.Content, "nameS", "name");
        }

        [Fact]
        public async Task TestClassMembersNameRanges()
        {
            const string source = @"
class C
{
    private int {|name_f:_f|};
    private int {|name_f1:_f1|}, {|name_f2:_f2|};
    private const int {|name_c:_c|};
    public {|nameCtor:C|}() { }
    ~{|nameDtor:C|}() { }
    public void {|nameM1:M1|}() { }
    public void {|nameM2:M2|}(int i, ref string s, params object[] array) { }
    public static implicit operator {|nameOpC:C|}(int i) { return null; }
    public static C operator {|nameOpPlus:+|}(C c1, C c2) { return null; }
    public int {|nameP:P|} { get; set; }
    public event EventHandler {|nameE:E|};
    public event EventHandler {|nameE1:E1|}, {|nameE2:E2|};
    public event EventHandler {|nameE3:E3|} { add { } remove { } }
    internal int {|nameThis:this|}[int index] => 42;
}
";

            var testFile = new TestFile("test.cs", source);

            var response = await GetCodeStructureAsync(testFile);

            var elementC = Assert.Single(response.Elements);

            AssertRange(elementC.Children[0], testFile.Content, "name_f", "name");
            AssertRange(elementC.Children[1], testFile.Content, "name_f1", "name");
            AssertRange(elementC.Children[2], testFile.Content, "name_f2", "name");
            AssertRange(elementC.Children[3], testFile.Content, "name_c", "name");
            AssertRange(elementC.Children[4], testFile.Content, "nameCtor", "name");
            AssertRange(elementC.Children[5], testFile.Content, "nameDtor", "name");
            AssertRange(elementC.Children[6], testFile.Content, "nameM1", "name");
            AssertRange(elementC.Children[7], testFile.Content, "nameM2", "name");
            AssertRange(elementC.Children[8], testFile.Content, "nameOpC", "name");
            AssertRange(elementC.Children[9], testFile.Content, "nameOpPlus", "name");
            AssertRange(elementC.Children[10], testFile.Content, "nameP", "name");
            AssertRange(elementC.Children[11], testFile.Content, "nameE", "name");
            AssertRange(elementC.Children[12], testFile.Content, "nameE1", "name");
            AssertRange(elementC.Children[13], testFile.Content, "nameE2", "name");
            AssertRange(elementC.Children[14], testFile.Content, "nameE3", "name");
            AssertRange(elementC.Children[15], testFile.Content, "nameThis", "name");
        }

        private static void AssertRange(CodeElement elementC, TestContent content, string contentSpanName, string elementRangeName)
        {
            var span = Assert.Single(content.GetSpans(contentSpanName));
            var range = content.GetRangeFromSpan(span).ToRange();
            Assert.Equal(range, elementC.Ranges[elementRangeName]);
        }

        private static void AssertElement(CodeElement element, string kind, string name, string displayName, string accessibility = null, bool? @static = null)
        {
            Assert.Equal(kind, element.Kind);
            Assert.Equal(name, element.Name);
            Assert.Equal(displayName, element.DisplayName);

            if (accessibility != null)
            {
                Assert.Equal(accessibility, element.Properties[CodeElementPropertyNames.Accessibility]);
            }

            if (@static != null)
            {
                Assert.Equal(@static, element.Properties[CodeElementPropertyNames.Static]);
            }
        }

        private Task<CodeStructureResponse> GetCodeStructureAsync(string source, OmniSharpTestHost host = null)
        {
            var testFile = new TestFile("test.cs", source);
            return GetCodeStructureAsync(testFile, host);
        }

        private Task<CodeStructureResponse> GetCodeStructureAsync(TestFile testFile, OmniSharpTestHost host = null)
        {
            host = host ?? SharedOmniSharpTestHost;

            host.AddFilesToWorkspace(testFile);

            var requestHandler = GetRequestHandler(host);

            var request = new CodeStructureRequest
            {
                FileName = "test.cs"
            };

            return requestHandler.Handle(request);
        }
    }
}
