using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Models.FindImplementations;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FindImplementationFacts : AbstractSingleRequestHandlerTestFixture<FindImplementationsService>
    {
        public FindImplementationFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.FindImplementations;

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task CanFindInterfaceTypeImplementation(string filename)
        {
            const string code = @"
                public interface Som$$eInterface {}
                public class SomeClass : SomeInterface {}";

            var implementations = await FindImplementationsAsync(code, filename);
            var implementation = implementations.First();

            Assert.Equal("SomeClass", implementation.Name);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task CanFindInterfaceMethodImplementation(string filename)
        {
            const string code = @"
                public interface SomeInterface { void Some$$Method(); }
                public class SomeClass : SomeInterface {
                    public void SomeMethod() {}
                }";

            var implementations = await FindImplementationsAsync(code, filename);
            var implementation = implementations.First();

            Assert.Equal("SomeMethod", implementation.Name);
            Assert.Equal("SomeClass", implementation.ContainingType.Name);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task CanFindOverride(string filename)
        {
            const string code = @"
                public class BaseClass { public abstract Some$$Method() {} }
                public class SomeClass : BaseClass
                {
                    public override SomeMethod() {}
                }";

            var implementations = await FindImplementationsAsync(code, filename);
            var implementation = implementations.First();

            Assert.Equal("SomeMethod", implementation.Name);
            Assert.Equal("SomeClass", implementation.ContainingType.Name);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task CanFindSubclass(string filename)
        {
            const string code = @"
                public abstract class BaseClass {}
                public class SomeClass : Base$$Class {}";

            var implementations = await FindImplementationsAsync(code, filename);
            var implementation = implementations.First();

            Assert.Equal("SomeClass", implementation.Name);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task CanFindTypeDeclaration(string filename)
        {
            const string code = @"
                public class MyClass 
                { 
                    public MyClass() { var other = new Other$$Class(); }
                }

                public class OtherClass 
                { 
                }";

            var implementations = await FindImplementationsAsync(code, filename);
            var implementation = implementations.First();

            Assert.Equal("OtherClass", implementation.Name);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task CanFindMethodDeclaration(string filename)
        {
            const string code = @"
                public class MyClass 
                { 
                    public MyClass() { Fo$$o(); }

                    public void Foo() {}
                }";

            var implementations = await FindImplementationsAsync(code, filename);
            var implementation = implementations.First();

            Assert.Equal("Foo", implementation.Name);
            Assert.Equal("MyClass", implementation.ContainingType.Name);
            Assert.Equal(SymbolKind.Method, implementation.Kind);
        }

        private async Task<IEnumerable<ISymbol>> FindImplementationsAsync(string code, string filename)
        {
            var testFile = new TestFile(filename, code);
            using (var host = CreateOmniSharpHost(testFile))
            {
                var point = testFile.Content.GetPointFromPosition();
                var requestHandler = GetRequestHandler(host);

                var request = new FindImplementationsRequest
                {
                    Line = point.Line,
                    Column = point.Offset,
                    FileName = testFile.FileName,
                    Buffer = testFile.Content.Code
                };

                var implementations = await requestHandler.Handle(request);

                return await SymbolsFromQuickFixesAsync(host.Workspace, implementations.QuickFixes);
            }
        }

        private async Task<IEnumerable<ISymbol>> SymbolsFromQuickFixesAsync(OmniSharpWorkspace workspace, IEnumerable<QuickFix> quickFixes)
        {
            var symbols = new List<ISymbol>();
            foreach (var quickfix in quickFixes)
            {
                var document = workspace.GetDocument(quickfix.FileName);
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(quickfix.Line, quickfix.Column));
                var semanticModel = await document.GetSemanticModelAsync();
                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, workspace);

                symbols.Add(symbol);
            }

            return symbols;
        }
    }
}
