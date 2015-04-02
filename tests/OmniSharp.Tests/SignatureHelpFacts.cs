using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
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
        System.Console.Clear();
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Null(actual);

            source =
@"class Program
{
    public static void Main(){
        System.Cons$ole.Clear();
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
        System.Console.Foo$Bar();
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
        System.Conso$le;
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
        System.Console.Clear($);
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Equal(1, actual.Signatures.Count());
            Assert.Equal(0, actual.ActiveParameter);
            Assert.Equal(0, actual.ActiveSignature);
            Assert.Equal("Clear", actual.Signatures.ElementAt(0).Name);
            Assert.Equal(0, actual.Signatures.ElementAt(0).Parameters.Count());
        }

        [Fact]
        public async Task SignatureWithOverloads()
        {
            var source =
@"class Program
{
    public static void Main(){
        new Program().Foo(12, $
    }
    
    private int Foo() 
    {
        return 3;
    }
    
    private int Foo(int m, int n)
    {
        return m * Foo() * n;
    }
}";
            var actual = await GetSignatureHelp(source);
            Assert.Equal(2, actual.Signatures.Count());
            Assert.Equal(1, actual.ActiveParameter);
            Assert.Equal(2, actual.Signatures.ElementAt(actual.ActiveSignature).Parameters.Count());
        }

        private async Task<SignatureHelp> GetSignatureHelp(string source)
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);
            source = source.Replace("$", string.Empty);

            var request = new Request()
            {
                FileName = "dummy.cs",
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                Buffer = source
            };

            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace, null);
            return await controller.GetSignatureHelp(request);
        }
    }
}