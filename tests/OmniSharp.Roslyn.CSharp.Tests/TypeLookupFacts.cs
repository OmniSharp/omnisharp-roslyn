using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Types;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class TypeLookupFacts
    {
        [Fact]
        public async Task OmitsNamespaceForNonRegularCSharpSyntax()
        {
            var source1 = @"class Foo {}";

            var workspace = TestHelpers.CreateCsxWorkspace(source1);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var response = await controller.Handle(new TypeLookupRequest { FileName = "dummy.csx", Line = 1, Column = 8 });

            Assert.Equal("Foo", response.Type);
        }

        [Fact]
        public async Task OmitsNamespaceForTypesInGlobalNamespace()
        {
            var source = @"namespace Bar {
            class Foo {}
            }
            class Baz {}";

            var workspace = await TestHelpers.CreateSimpleWorkspace(source);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var responseInNormalNamespace = await controller.Handle(new TypeLookupRequest { FileName = "dummy.cs", Line = 2, Column = 20 });
            var responseInGlobalNamespace = await controller.Handle(new TypeLookupRequest { FileName = "dummy.cs", Line = 4, Column = 20 });

            Assert.Equal("Bar.Foo", responseInNormalNamespace.Type);
            Assert.Equal("Baz", responseInGlobalNamespace.Type);
        }

        [Fact]
        public async Task IncludesNamespaceForRegularCSharpSyntax()
        {
            var source1 = @"namespace Bar {
            class Foo {}
            }";

            var workspace = await TestHelpers.CreateSimpleWorkspace(source1);

            var controller = new TypeLookupService(workspace, new FormattingOptions());
            var response = await controller.Handle(new TypeLookupRequest { FileName = "dummy.cs", Line = 2, Column = 20 });

            Assert.Equal("Bar.Foo", response.Type);
        }
    }
}
