using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.SignatureHelp;
using OmniSharp.Roslyn.CSharp.Services.Signatures;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class SignatureHelpFacts : AbstractSingleRequestHandlerTestFixture<SignatureHelpService>
    {
        public SignatureHelpFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.SignatureHelp;

        [Fact]
        public async Task NoInvocationNoHelp1()
        {
            const string source =
@"class Program
{
    public static void Ma$$in(){
        System.Guid.NoSuchMethod();
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Null(actual);
        }

        [Fact]
        public async Task NoInvocationNoHelp2()
        {
            const string source =
@"class Program
{
    public static void Main(){
        System.Gu$$id.NoSuchMethod();
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Null(actual);
        }

        [Fact]
        public async Task NoInvocationNoHelp3()
        {
            const string source =
@"class Program
{
    public static void Main(){
        System.Guid.NoSuchMethod()$$;
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Null(actual);
        }

        [Fact]
        public async Task NoTypeNoHelp()
        {
            const string source =
@"class Program
{
    public static void Main(){
        System.Guid.Foo$$Bar();
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Null(actual);
        }

        [Fact]
        public async Task NoMethodNoHelp()
        {
            const string source =
@"class Program
{
    public static void Main(){
        System.Gu$$id;
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Null(actual);
        }

        [Fact]
        public async Task SimpleSignatureHelp()
        {
            const string source =
@"class Program
{
    public static void Main(){
        System.Guid.NewGuid($$);
    }
}";

            var actual = await GetSignatureHelp(source);
            Assert.Equal(1, actual.Signatures.Count());
            Assert.Equal(0, actual.ActiveParameter);
            Assert.Equal(0, actual.ActiveSignature);
            Assert.Equal("NewGuid", actual.Signatures.ElementAt(0).Name);
            Assert.Equal(0, actual.Signatures.ElementAt(0).Parameters.Count());
        }

        [Fact]
        public async Task TestForParameterLabels()
        {
            const string source =
@"class Program
{
    public static void Main(){
        Foo($$);
    }
    pubic static Foo(bool b, int n = 1234)
    {
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Equal(1, actual.Signatures.Count());
            Assert.Equal(0, actual.ActiveParameter);
            Assert.Equal(0, actual.ActiveSignature);

            var signature = actual.Signatures.ElementAt(0);
            Assert.Equal(2, signature.Parameters.Count());
            Assert.Equal("b", signature.Parameters.ElementAt(0).Name);
            Assert.Equal("bool b", signature.Parameters.ElementAt(0).Label);
            Assert.Equal("n", signature.Parameters.ElementAt(1).Name);
            Assert.Equal("int n = 1234", signature.Parameters.ElementAt(1).Label);
        }

        [Fact]
        public async Task ActiveParameterIsBasedOnComma1()
        {
            // 1st position, a
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(1$$2,
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Equal(0, actual.ActiveParameter);
        }

        [Fact]
        public async Task ActiveParameterIsBasedOnComma2()
        {
            // 1st position, b
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12 $$)
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Equal(0, actual.ActiveParameter);
        }

        [Fact]
        public async Task ActiveParameterIsBasedOnComma3()
        {
            // 2nd position, a
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12, $$
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Equal(1, actual.ActiveParameter);
        }

        [Fact]
        public async Task ActiveParameterIsBasedOnComma4()
        {
            // 2nd position, b
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12, 1$$
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Equal(1, actual.ActiveParameter);
        }

        [Fact]
        public async Task ActiveParameterIsBasedOnComma5()
        {
            // 3rd position, a
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12, 1, $$
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Equal(2, actual.ActiveParameter);
        }

        [Fact]
        public async Task ActiveSignatureIsBasedOnTypes1()
        {
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12, $$
    }
    /// foo1
    private int Foo()
    {
        return 3;
    }
    /// foo2
    private int Foo(int m, int n)
    {
        return m * Foo() * n;
    }
    /// foo3
    private int Foo(string m, int n)
    {
        return Foo(m.length, n);
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Equal(3, actual.Signatures.Count());
            Assert.Contains("foo2", actual.Signatures.ElementAt(actual.ActiveSignature).Documentation);
        }

        [Fact]
        public async Task ActiveSignatureIsBasedOnTypes2()
        {
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo(""d"", $$
    }
    /// foo1
    private int Foo()
    {
        return 3;
    }
    /// foo2
    private int Foo(int m, int n)
    {
        return m * Foo() * n;
    }
    /// foo3
    private int Foo(string m, int n)
    {
        return Foo(m.length, n);
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Equal(3, actual.Signatures.Count());
            Assert.Contains("foo3", actual.Signatures.ElementAt(actual.ActiveSignature).Documentation);
        }

        [Fact]
        public async Task ActiveSignatureIsBasedOnTypes3()
        {
            const string source =
@"class Program
{
    public static void Main(){
        new Program().Foo($$)
    }
    /// foo1
    private int Foo()
    {
        return 3;
    }
    /// foo2
    private int Foo(int m, int n)
    {
        return m * Foo() * n;
    }
    /// foo3
    private int Foo(string m, int n)
    {
        return Foo(m.length, n);
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Equal(3, actual.Signatures.Count());
            Assert.Contains("foo1", actual.Signatures.ElementAt(actual.ActiveSignature).Documentation);
        }

        [Fact]
        public async Task SigantureHelpForCtor()
        {
            const string source =
@"class Program
{
    public static void Main()
    {
        new Program($$)
    }
    public Program()
    {
    }
    public Program(bool b)
    {
    }
    public Program(Program p)
    {
    }
}";

            var actual = await GetSignatureHelp(source);
            Assert.Equal(3, actual.Signatures.Count());
        }

        [Fact]
        public async Task SigantureHelpForCtorWithOverloads()
        {
            const string source =
@"class Program
{
    public static void Main()
    {
        new Program(true, 12$$3)
    }
    public Program()
    {
    }
    /// ctor2
    public Program(bool b, int n)
    {
    }
    public Program(Program p, int n)
    {
    }
}";

            var actual = await GetSignatureHelp(source);
            Assert.Equal(3, actual.Signatures.Count());
            Assert.Equal(1, actual.ActiveParameter);
            Assert.Contains("ctor2", actual.Signatures.ElementAt(actual.ActiveSignature).Documentation);
        }

        [Fact]
        public async Task SkipReceiverOfExtensionMethods()
        {
            const string source =
@"class Program
{
    public static void Main()
    {
        new Program().B($$);
    }
    public Program()
    {
    }
    public bool B(this Program p, int n)
    {
        return p.Foo() > n;
    }
}";

            var actual = await GetSignatureHelp(source);
            Assert.Equal(1, actual.Signatures.Count());
            Assert.Equal(1, actual.Signatures.ElementAt(actual.ActiveSignature).Parameters.Count());
            Assert.Equal("n", actual.Signatures.ElementAt(actual.ActiveSignature).Parameters.ElementAt(0).Name);
        }

        private async Task<SignatureHelpResponse> GetSignatureHelp(string source)
        {
            var testFile = new TestFile("dummy.cs", source);
            using (var host = CreateOmniSharpHost(testFile))
            {
                var point = testFile.Content.GetPointFromPosition();

                var request = new SignatureHelpRequest()
                {
                    FileName = testFile.FileName,
                    Line = point.Line,
                    Column = point.Offset,
                    Buffer = testFile.Content.Code
                };

                var requestHandler = GetRequestHandler(host);

                return await requestHandler.Handle(request);
            }
        }
    }
}
