using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FindImplementationFacts : AbstractTestFixture
    {
        public FindImplementationFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task CanFindInterfaceTypeImplementation()
        {
            const string source = @"
                public interface Som$$eInterface {}
                public class SomeClass : SomeInterface {}";

            var implementations = await FindImplementations(source);
            var implementation = implementations.First();

            Assert.Equal("SomeClass", implementation.Name);
        }

        [Fact]
        public async Task CanFindInterfaceMethodImplementation()
        {
            const string source = @"
                public interface SomeInterface { void Some$$Method(); }
                public class SomeClass : SomeInterface {
                    public void SomeMethod() {}
                }";

            var implementations = await FindImplementations(source);
            var implementation = implementations.First();
            Assert.Equal("SomeMethod", implementation.Name);
            Assert.Equal("SomeClass", implementation.ContainingType.Name);
        }

        [Fact]
        public async Task CanFindOverride()
        {
            const string source = @"
                public class BaseClass { public abstract Some$$Method() {} }
                public class SomeClass : BaseClass
                {
                    public override SomeMethod() {}
                }";

            var implementations = await FindImplementations(source);
            var implementation = implementations.First();

            Assert.Equal("SomeMethod", implementation.Name);
            Assert.Equal("SomeClass", implementation.ContainingType.Name);
        }

        [Fact]
        public async Task CanFindSubclass()
        {
            const string source = @"
                public class BaseClass {}
                public class SomeClass : Base$$Class {}";

            var implementations = await FindImplementations(source);
            var implementation = implementations.First();

            Assert.Equal("SomeClass", implementation.Name);
        }

        private async Task<IEnumerable<ISymbol>> FindImplementations(string input)
        {
            var testFile = new TestFile("dummy.cs", input);
            var point = testFile.Content.GetPointFromPosition();

            var workspace = await CreateWorkspaceAsync(testFile);
            var controller = new FindImplementationsService(workspace);

            var request = new FindImplementationsRequest
            {
                Line = point.Line,
                Column = point.Offset,
                FileName = testFile.FileName,
                Buffer = testFile.Content.Code
            };

            var implementations = await controller.Handle(request);

            return await SymbolsFromQuickFixesAsync(workspace, implementations.QuickFixes);
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
