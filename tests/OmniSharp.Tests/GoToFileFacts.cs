using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{    
    public class TypeLookupFacts
    {
        [Fact]
        public async Task OmitsNamespaceForNonRegularCSharpSyntax()
        {
            var source1 = @"class Foo {}";
            
            var workspace = TestHelpers.CreateCsxWorkspace(source1);
            
            var controller = new OmnisharpController(workspace, null);
            var response = await controller.TypeLookup(new TypeLookupRequest { FileName = "dummy.csx", Line = 1, Column = 8 });
            
            Assert.Equal("Foo", response.Type);   
        } 
        
        [Fact]
        public async Task IncludesNamespaceForRegularCSharpSyntax()
        {
            var source1 = @"namespace Bar {
            class Foo {}
            }";
            
            var workspace = TestHelpers.CreateSimpleWorkspace(source1);
            
            var controller = new OmnisharpController(workspace, null);
            var response = await controller.TypeLookup(new TypeLookupRequest { FileName = "dummy.cs", Line = 2, Column = 20 });
            
            Assert.Equal("Bar.Foo", response.Type);   
        } 
    }
        
    public class GoToFileFacts
    {
        [Fact]
        public void ReturnsAListOfAllWorkspaceFiles()
        {
            var source1 = @"class Foo {}";
            var source2 = @"class Bar {}";
            
            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                { "foo.cs", source1 }, { "bar.cs", source2}
            });
            var controller = new OmnisharpController(workspace, null);
            var response = controller.GoToFile(new Request());
            
            Assert.Equal(2, response.QuickFixes.Count());   
            Assert.Equal("foo.cs", response.QuickFixes.ElementAt(0).FileName);  
            Assert.Equal("bar.cs", response.QuickFixes.ElementAt(1).FileName);       
        }   
        
        [Fact]
        public void ReturnsEmptyResponseForEmptyWorskpace()
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>());
            var controller = new OmnisharpController(workspace, null);
            var response = controller.GoToFile(new Request());
            
            Assert.Equal(0, response.QuickFixes.Count());   
        } 
    }
}