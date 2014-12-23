using System.Linq;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class FindImplementationFacts
    {
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
        
        [Fact]
        public async void CanFindInterfaceTypeImplementation()
        {
            var source = @"
                public interface Som$eInterface {}
                public class SomeClass : SomeInterface {}";

            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace);
            var request = CreateRequest(source);
            var implementations = await controller.FindImplementations(request);
            var result = implementations.QuickFixes.First();
            var symbol = await TestHelpers.SymbolFromQuickFix(workspace, result);
            Assert.Equal("SomeClass", symbol.Name);
        }

        [Fact]
        public async void CanFindInterfaceMethodImplementation()
        {
            var source = @"
                public interface SomeInterface { void Some$Method(); }
                public class SomeClass : SomeInterface {
                    public void SomeMethod() {}
                }";

            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace);
            var request = CreateRequest(source);
            var implementations = await controller.FindImplementations(request);
            var result = implementations.QuickFixes.First();
            var symbol = await TestHelpers.SymbolFromQuickFix(workspace, result);
            Assert.Equal("SomeMethod", symbol.Name);
            Assert.Equal("SomeClass", symbol.ContainingType.Name);
        }

        [Fact]
        public async void CanFindOverride()
        {
            var source = @"
                public class BaseClass { public abstract Some$Method() {} }
                public class SomeClass : BaseClass
                {
                    public override SomeMethod() {}
                }";

            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace);
            var request = CreateRequest(source);
            var implementations = await controller.FindImplementations(request);
            var result = implementations.QuickFixes.First();
            var symbol = await TestHelpers.SymbolFromQuickFix(workspace, result);
            Assert.Equal("SomeMethod", symbol.Name);
            Assert.Equal("SomeClass", symbol.ContainingType.Name);
        }
    }
}