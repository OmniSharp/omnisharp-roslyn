using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Signatures;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class SignatureHelpFacts
    {
        [Fact]
        public async Task NoInvocationNoHelp()
        {
            var source =
@"class Program
{
    public static void Ma$in(){
        System.Guid.NoSuchMethod();
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Null(actual);

            source =
@"class Program
{
    public static void Main(){
        System.Gu$id.NoSuchMethod();
    }
}";
            actual = await GetSignatureHelp(source);
            Assert.Null(actual);

            source =
@"class Program
{
    public static void Main(){
        System.Guid.NoSuchMethod()$;
    }
}";
            actual = await GetSignatureHelp(source);
            Assert.Null(actual);
        }

        [Fact]
        public async Task NoTypeNoHelp()
        {
            var source =
@"class Program
{
    public static void Main(){
        System.Guid.Foo$Bar();
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Null(actual);
        }

        [Fact]
        public async Task NoMethodNoHelp()
        {
            var source =
@"class Program
{
    public static void Main(){
        System.Gu$id;
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Null(actual);
        }

        [Fact]
        public async Task SimpleSignatureHelp()
        {
            var source =
@"class Program
{
    public static void Main(){
        System.Guid.NewGuid($);
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
            var source =
@"class Program
{
    public static void Main(){
        Foo($);
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
        public async Task ActiveParameterIsBasedOnComma()
        {
            // 1st position, a
            var source =
@"class Program
{
    public static void Main(){
        new Program().Foo(1$2,
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Equal(0, actual.ActiveParameter);

            // 1st position, b
            source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12 $)
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            actual = await GetSignatureHelp(source);
            Assert.Equal(0, actual.ActiveParameter);

            // 2nd position, a
            source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12, $
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            actual = await GetSignatureHelp(source);
            Assert.Equal(1, actual.ActiveParameter);

            // 2nd position, b
            source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12, 1$
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            actual = await GetSignatureHelp(source);
            Assert.Equal(1, actual.ActiveParameter);

            // 3rd position, a
            source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12, 1, $
    }
    /// foo1
    private int Foo(int one, int two, int three)
    {
        return 3;
    }
}";
            actual = await GetSignatureHelp(source);
            Assert.Equal(2, actual.ActiveParameter);
        }

        [Fact]
        public async Task ActiveSignatureIsBasedOnTypes()
        {
            var source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12, $
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
            Assert.True(actual.Signatures.ElementAt(actual.ActiveSignature).Documentation.Contains("foo2"));

            source =
@"class Program
{
    public static void Main(){
        new Program().Foo(""d"", $
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
            actual = await GetSignatureHelp(source);
            Assert.Equal(3, actual.Signatures.Count());
            Assert.True(actual.Signatures.ElementAt(actual.ActiveSignature).Documentation.Contains("foo3"));

            source =
@"class Program
{
    public static void Main(){
        new Program().Foo($)
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
            actual = await GetSignatureHelp(source);
            Assert.Equal(3, actual.Signatures.Count());
            Assert.True(actual.Signatures.ElementAt(actual.ActiveSignature).Documentation.Contains("foo1"));
        }

        [Fact]
        public async Task SigantureHelpForCtor()
        {
            var source =
@"class Program
{
    public static void Main()
    {
        new Program($)
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
            var source =
@"class Program
{
    public static void Main()
    {
        new Program(true, 12$3)
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
            Assert.True(actual.Signatures.ElementAt(actual.ActiveSignature).Documentation.Contains("ctor2"));
        }

        [Fact]
        public async Task SkipReceiverOfExtensionMethods()
        {
            var source =
@"class Program
{
    public static void Main()
    {
        new Program().B($);
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

        private async Task<SignatureHelp> GetSignatureHelp(string source)
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);
            source = source.Replace("$", string.Empty);

            var request = new SignatureHelpRequest()
            {
                FileName = "dummy.cs",
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                Buffer = source
            };

            var workspace = await TestHelpers.CreateSimpleWorkspace(source);
            var controller = new SignatureHelpService(workspace);
            return await controller.Handle(request);
        }
    }
}
