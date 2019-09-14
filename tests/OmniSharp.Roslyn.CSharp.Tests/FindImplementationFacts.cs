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
        public FindImplementationFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
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
        public async Task CanFindInterfaceMethodOverride(string filename)
        {
            const string code = @"
                public interface SomeInterface { void Some$$Method(); }
                public class SomeClass : SomeInterface {
                    public virtual void SomeMethod() {}
                }
                public class SomeChildClass : SomeClass {
                    public override void SomeMethod() {}
                }";

            var implementations = await FindImplementationsAsync(code, filename);

            Assert.Equal(2, implementations.Count());
            Assert.True(implementations.Any(x => x.ContainingType.Name == "SomeClass" && x.Name == "SomeMethod"), "Couldn't find SomeClass.SomeMethod in the discovered implementations");
            Assert.True(implementations.Any(x => x.ContainingType.Name == "SomeChildClass" && x.Name == "SomeMethod"), "Couldn't find SomeChildClass.SomeMethod in the discovered implementations");
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task CanFindOverride(string filename)
        {
            const string code = @"
                public abstract class BaseClass { public abstract void Some$$Method(); }
                public class SomeClass : BaseClass
                {
                    public override void SomeMethod() {}
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

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task CanFindPartialClass(string filename)
        {
            const string code = @"
                public partial class Some$$Class { void SomeMethod() {} }
                public partial class SomeClass { void AnotherMethod() {} }";

            var implementations = await FindImplementationsAsync(code, filename);

            Assert.Equal(2, implementations.Count());
            Assert.True(implementations.All(x => x.Name == "SomeClass"));
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task CanFindPartialMethod(string filename)
        {
            const string code = @"
                public partial class SomeClass { partial void Some$$Method(); }
                public partial class SomeClass { partial void SomeMethod() { /* this is implementation of the partial method */ } }";

            var implementations = await FindImplementationsAsync(code, filename);

            Assert.Single(implementations);

            var implementation = implementations.First();
            Assert.Equal("SomeMethod", implementation.Name);

            // Assert that the actual implementation part is returned.
            Assert.True(implementation is IMethodSymbol method && method.PartialDefinitionPart != null && method.PartialImplementationPart == null);
        }

        private async Task<IEnumerable<ISymbol>> FindImplementationsAsync(string code, string filename)
        {
            var testFile = new TestFile(filename, code);
            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);
            var point = testFile.Content.GetPointFromPosition();
            var requestHandler = GetRequestHandler(SharedOmniSharpTestHost);

            var request = new FindImplementationsRequest
            {
                Line = point.Line,
                Column = point.Offset,
                FileName = testFile.FileName,
                Buffer = testFile.Content.Code
            };

            var implementations = await requestHandler.Handle(request);

            return await SymbolsFromQuickFixesAsync(SharedOmniSharpTestHost.Workspace, implementations.QuickFixes);
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
