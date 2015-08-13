using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Filters;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{    
    public class GoToRegionFacts
    {
        [Fact]
        public async Task CanFindRegionsInFileWithRegions()
        {
            var source = @"
                public class Foo 
                {
                      #region A
                      public string A$Property {get; set;}
                      #endregion
                      
                      #region B
                      public string BProperty {get; set;}
                      #endregion
                }";
            
            var regions = await FindRegions(source);

            Assert.Equal(4, regions.QuickFixes.Count());   
            Assert.Equal("#region A", regions.QuickFixes.ElementAt(0).Text);
            Assert.Equal(4, regions.QuickFixes.ElementAt(0).Line);  
            Assert.Equal("#endregion", regions.QuickFixes.ElementAt(1).Text);
            Assert.Equal(6, regions.QuickFixes.ElementAt(1).Line);  
            Assert.Equal("#region B", regions.QuickFixes.ElementAt(2).Text);
            Assert.Equal(8, regions.QuickFixes.ElementAt(2).Line);  
            Assert.Equal("#endregion", regions.QuickFixes.ElementAt(3).Text);     
            Assert.Equal(10, regions.QuickFixes.ElementAt(3).Line);      
        }
        
        [Fact]
        public async Task DoesNotFindRegionsInFileWithoutRegions()
        {
            var source = @"public class Fo$o{}";
            var regions = await FindRegions(source);
            Assert.Equal(0, regions.QuickFixes.Count());        
        }        
        
        private async Task<QuickFixResponse> FindRegions(string source)
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(source);
            var controller = new OmnisharpController(workspace, new FakeOmniSharpOptions());
            var request = CreateRequest(source);
            var bufferFilter = new UpdateBufferFilter(workspace);
            bufferFilter.OnActionExecuting(TestHelpers.CreateActionExecutingContext(request, controller));
            return await controller.GoToRegion(request);
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