using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class FindImplementationFacts
    {
        [Fact]
        public async Task CanFindInterfaceTypeImplementation()
        {
            var source = @"
                public interface Som$eInterface {}
                public class SomeClass : SomeInterface {}";

            var implementations = await FindImplementations(source);
            var implementation = implementations.First();

            Assert.Equal("SomeClass", implementation.Name);
        }

        [Fact]
        public async Task CanFindInterfaceMethodImplementation()
        {
            var source = @"
                public interface SomeInterface { void Some$Method(); }
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
            var source = @"
                public class BaseClass { public abstract Some$Method() {} }
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
            var source = @"
                public class BaseClass {}
                public class SomeClass : Base$Class {}";

            var implementations = await FindImplementations(source);
            var implementation = implementations.First();

            Assert.Equal("SomeClass", implementation.Name);
        }

        private async Task<IEnumerable<ISymbol>> FindImplementations(string source)
        {
            var workspace = await TestHelpers.CreateSimpleWorkspace(source);
            var controller = new FindImplementationsService(workspace);
            var request = CreateRequest(source);

            await workspace.BufferManager.UpdateBuffer(request);

            var implementations = await controller.Handle(request);
            return await TestHelpers.SymbolsFromQuickFixes(workspace, implementations.QuickFixes);
        }

        private FindImplementationsRequest CreateRequest(string source, string fileName = "dummy.cs")
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);
            return new FindImplementationsRequest {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                FileName = fileName,
                Buffer = source.Replace("$", "")
            };
        }

    }
}
