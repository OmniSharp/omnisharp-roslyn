using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Filters;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
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
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace, null);
            var request = CreateRequest(source);
            var bufferFilter = new UpdateBufferFilter(workspace);
            bufferFilter.OnActionExecuting(TestHelpers.CreateActionExecutingContext(request, controller));
            var implementations = await controller.FindImplementations(request);
            return await TestHelpers.SymbolsFromQuickFixes(workspace, implementations.QuickFixes);
        }

        private Request CreateRequest(string source, string fileName = "dummy.cs")
        {
            var lineColumn = TestHelpers.GetLineAndColumnFromDollar(source);
            return new Request {
                Line = lineColumn.Line,
                Column = lineColumn.Column,
                FileName = fileName,
                Buffer = source.Replace("$", "")
            };
        }
        
    }
}