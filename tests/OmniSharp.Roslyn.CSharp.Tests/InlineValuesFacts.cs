using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models.v1.InlineValues;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn.CSharp.Services.InlineValues;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class InlineValuesFacts : AbstractSingleRequestHandlerTestFixture<InlineValuesService>
    {
        public InlineValuesFacts(ITestOutputHelper testOutput, SharedOmniSharpHostFixture sharedOmniSharpHostFixture) : base(testOutput, sharedOmniSharpHostFixture)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.InlineValues;

        [Fact]
        public async Task GlobalStatementValues()
        {
            await AssertPresent(
@"{|viewport:int {|a:a|} = 1;
{|a:a|} = 2;
{|context:_ = {|C.D:C.D|}|};
_ = {|C.E:C.E|};
_ = {|args:args|};

class C
{
    public static int D = 1;
    public static int E { get; set; }
}|}");
        }

        [Fact]
        public async Task GlobalStatementValuesInLocalFunction()
        {
            await AssertPresent(
@"{|viewport:int a = 1;
a = 2;
_ = C.D;
_ = C.E;
_ = args;

void M()
{
    {|context:_ = {|a:a|}|};
    int {|b:b|} = 1;
    {|b:b|} = 2;
    _ = {|C.D:C.D|};
    _ = {|C.E:C.E|};
    _ = {|args:args|}[0].ToString();
}

class C
{
    public static int D = 1;
    public static int E { get; set; }
}|}");
        }

        [Fact]
        public async Task RegularMethod()
        {
            await AssertPresent(@"
{|viewport:class C
{
    string s1;
    string S2 { get; set; }
    void M(int a)
    {
        {|context:_ = {|a:a|}|};
        _ = {|this.s1:s1|};
        _ = {|this.S2:S2|};
        int {|b:b|} = 1;
        _ = {|b:b|};
    }
        
}|}");
        }

        [Fact]
        public async Task ForeachDeclaration()
        {
            await AssertPresent(@"{|viewport:{|context:foreach ({|x:var|} x in new int[10]) {}|}|}");
        }

        [Fact]
        public async Task UsingDeclarations()
        {
            await AssertPresent(
@"{|viewport:{|context:using (var {|d1:d1|} = (System.IDisposable)null)
{
    using var {|d2:d2|} = (System.IDisposable)null;
}|}|}");
        }

        [Theory]
        [InlineData("() => { int b = 1; }")]
        [InlineData("delegate { int b = 1; }")]
        public async Task ContextOnAnonymousFunctions(string anonymousFunction)
        {
            await AssertPresent(@"
{|viewport:using System;
Action {|a:a|};
{|a:a|} = {|context:" + anonymousFunction + @"|};|}
");
        }

        [Fact]
        public async Task StaticFieldsAndPropertiesOnGenericContainer()
        {
            await AssertPresent(@"
{|viewport:class C<T>
{
    static string Field;
    static string Property { get; set; }

    void M()
    {
        _ = {|context:Field|};
        _ = Property;
    }
}|}");
        }

        [Fact]
        public async Task StaticFieldsAndPropertiesOnGenericContainer_Nested()
        {
            await AssertPresent(@"
{|viewport:class C<T>
{
    class D
    {
        static string Field;
        static string Property { get; set; }
        void M()
        {
            _ = {|context:Field|};
            _ = Property;
        }
    }
}|}");
        }

        [Fact]
        public async Task ShadowedMembers()
        {
            await AssertPresent(@"
{|viewport:class C
{
    static int field1;
    int field2;
    static int property1 { get; set; }
    int property2 { get; set; }

    void M(int field1, int property1)
    {
        System.Action<int> a = (property1) =>
        {
            int {|field2:field2|} = {|context:{|this.field2:this.field2|}|};
            int {|property2:property2|} = {|this.property2:this.property2|};
            {|field1:field1|} = {|C.field1:C.field1|};
            {|property1:property1|} = {|C.property1:C.property1|};
}|}");
        }

        [Fact]
        public async Task OnLambdaOpenBrace()
        {
            await AssertPresent(@"
{|viewport:using System;
Action a = () =>
{|context:{ |}
    int {|a:a|};
};|}");
        }

        [Fact]
        public async Task OnMethodOpenBrace()
        {
            await AssertPresent(@"
{|viewport:class C
{
    void M()
    {|context:{ |}
        int {|a:a|} = 1;
    }
}|}");
        }

        [Fact]
        public async Task OutVariableDeclaration()
        {
            await AssertPresent(@"
{|viewport:{|context:M(out var {|a:a|})|};

void M(out int i) => i = 1;|}");
        }

        private async Task AssertPresent(string code)
        {
            var (result, testFile) = await GetResults(code, "test.cs");

            var expected = new List<InlineValue>();
            foreach (var (name, span) in testFile.Content.GetNamesAndSpans())
            {
                if (name is "context" or "viewport") continue;

                expected.Add(new InlineValue
                {
                    Kind = InlineValueKind.EvaluatableExpression,
                    Text = name,
                    Range = ToRange(span, testFile)
                });
            }

            AssertEx.Equal(expected, result.Values);
        }

        private async Task<(InlineValuesResponse, TestFile)> GetResults(string code, string fileName)
        {
            var testFile = new TestFile(fileName, code);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var request = new InlineValuesRequest
            {
                FileName = fileName,
                Context = new InlineValuesContext
                {
                    FrameId = 0,
                    StoppedLocation = ToRange(testFile.Content.GetSpans("context").Single(), testFile)
                },
                ViewPort = ToRange(testFile.Content.GetSpans("viewport").Single(), testFile)
            };

            return (await GetRequestHandler(SharedOmniSharpTestHost).Handle(request), testFile);
        }

        private static Range ToRange(TextSpan span, TestFile testFile)
        {
            var textRange = testFile.Content.GetRangeFromSpan(span);
            return new()
            {
                Start = new() { Line = textRange.Start.Line, Column = textRange.Start.Offset },
                End = new() { Line = textRange.End.Line, Column = textRange.End.Offset }
            };
        }
    }
}
