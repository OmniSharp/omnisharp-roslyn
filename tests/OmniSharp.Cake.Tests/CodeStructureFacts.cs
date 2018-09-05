using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Services.RequestHandlers.Diagnostics;
using OmniSharp.Cake.Services.RequestHandlers.Structure;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Models.V2;
using OmniSharp.Models.V2.CodeStructure;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public class CodeStructureFacts : CakeSingleRequestHandlerTestFixture<CodeStructureHandler>
    {
        private readonly ILogger _logger;

        public CodeStructureFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
            _logger = LoggerFactory.CreateLogger<CodeStructureHandler>();
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

            var (response, _) = await GetCodeStructureAsync(source);

            Assert.Equal(5, response.Elements.Count);
            AssertElement(response.Elements[0], SymbolKinds.Class, "C", "C");
            AssertElement(response.Elements[1], SymbolKinds.Delegate, "D", "D");
            AssertElement(response.Elements[2], SymbolKinds.Enum, "E", "E");
            AssertElement(response.Elements[2].Children[0], SymbolKinds.EnumMember, "One", "One");
            AssertElement(response.Elements[2].Children[1], SymbolKinds.EnumMember, "Two", "Two");
            AssertElement(response.Elements[2].Children[2], SymbolKinds.EnumMember, "Three", "Three");
            AssertElement(response.Elements[3], SymbolKinds.Interface, "I", "I");
            AssertElement(response.Elements[4], SymbolKinds.Struct, "S", "S");
        }

        [Fact]
        public async Task AllTypesWithLoadedFile()
        {
            const string source = @"
#load foo.cake
class C { }
delegate void D(int i, ref string s);
enum E { One, Two, Three }
interface I { }
struct S { }
";

            var (response, _) = await GetCodeStructureAsync(source);

            Assert.Equal(5, response.Elements.Count);
            AssertElement(response.Elements[0], SymbolKinds.Class, "C", "C");
            AssertElement(response.Elements[1], SymbolKinds.Delegate, "D", "D");
            AssertElement(response.Elements[2], SymbolKinds.Enum, "E", "E");
            AssertElement(response.Elements[3], SymbolKinds.Interface, "I", "I");
            AssertElement(response.Elements[4], SymbolKinds.Struct, "S", "S");
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

            var (response, testFile) = await GetCodeStructureAsync(source);

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

        [Fact]
        public async Task TestClassMembersNameRangesWithLoadedFile()
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
#load foo.cake
    internal int {|nameThis:this|}[int index] => 42;
}
";

            var (response, testFile) = await GetCodeStructureAsync(source);

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
                Assert.Equal(accessibility, element.Properties[SymbolPropertyNames.Accessibility]);
            }

            if (@static != null)
            {
                Assert.Equal(@static, element.Properties[SymbolPropertyNames.Static]);
            }
        }

        private async Task<(CodeStructureResponse, TestFile)> GetCodeStructureAsync(string contents)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var testFile = new TestFile(Path.Combine(testProject.Directory, "build.cake"), contents);

                var request = new CodeStructureRequest
                {
                    FileName = testFile.FileName
                };

                var updateBufferRequest = new UpdateBufferRequest
                {
                    Buffer = testFile.Content.Code,
                    FileName = testFile.FileName,
                    FromDisk = false
                };

                await GetUpdateBufferHandler(host).Handle(updateBufferRequest);

                var requestHandler = GetRequestHandler(host);

                return (await requestHandler.Handle(request), testFile);
            }
        }
    }
}
