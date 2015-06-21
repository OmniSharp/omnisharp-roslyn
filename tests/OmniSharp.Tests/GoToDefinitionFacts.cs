using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class GoToDefinitionFacts
    {
        [Fact]
        public async Task ReturnsLocationSourceDefinition()
        {
            var source1 = @"using System;

class Foo {
}";
            var source2 = @"class Bar {
    private Foo foo;
}";

            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                { "foo.cs", source1 }, { "bar.cs", source2}
            });
            var controller = new OmnisharpController(workspace, null);
            var response = await controller.GotoDefinition(new Request
            {
                FileName = "bar.cs",
                Line = 2,
                Column = 14
            }) as ObjectResult;

            var definitionResponse = response.Value as GotoDefinitionResponse;

            Assert.Equal("foo.cs", definitionResponse.FileName);
            Assert.Equal(3, definitionResponse.Line);
            Assert.Equal(7, definitionResponse.Column);
        }

        [Fact]
        public async Task ReturnsEmptyResultWhenDefinitionIsNotFound()
        {
            var source1 = @"using System;

class Foo {
}";
            var source2 = @"class Bar {
    private Baz foo;
}";

            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                { "foo.cs", source1 }, { "bar.cs", source2}
            });
            var controller = new OmnisharpController(workspace, null);
            var response = await controller.GotoDefinition(new Request
            {
                FileName = "bar.cs",
                Line = 2,
                Column = 14
            }) as ObjectResult;

            var definitionResponse = response.Value as GotoDefinitionResponse;

            Assert.Null(definitionResponse.FileName);
            Assert.Equal(0, definitionResponse.Line);
            Assert.Equal(0, definitionResponse.Column);
        }
        
        [Fact]
        public async Task ReturnsPositionInMetadata_WhenSymbolIsMethod()
        {
            var controller = new OmnisharpController(CreateTestWorkspace(), null);
            var response = await controller.GotoDefinition(new Request
            {
                FileName = "bar.cs",
                Line = 7,
                Column = 20
            }) as ObjectResult;

            var definitionResponse = response.Value as GotoDefinitionResponse;

            Assert.Null(definitionResponse.FileName);
            Assert.Equal(0, definitionResponse.Line);
            Assert.Equal(0, definitionResponse.Column);
            Assert.Contains("public static class Console", definitionResponse.MetadataSource);
            Assert.Contains("public static void WriteLine(string value)", definitionResponse.MetadataSource);
        }

        [Fact]
        public async Task ReturnsPositionInMetadata_WhenSymbolIsExtensionMethod()
        {
            var controller = new OmnisharpController(CreateTestWorkspace(), null);
            var response = await controller.GotoDefinition(new Request
            {
                FileName = "bar.cs",
                Line = 10,
                Column = 17
            }) as ObjectResult;

            var definitionResponse = response.Value as GotoDefinitionResponse;

            Assert.Null(definitionResponse.FileName);
            Assert.Equal(0, definitionResponse.Line);
            Assert.Equal(0, definitionResponse.Column);
            Assert.Contains("public static class Enumerable", definitionResponse.MetadataSource);
            Assert.Contains("public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source)", definitionResponse.MetadataSource);
        }

        [Fact]
        public async Task ReturnsPositionInMetadata_WhenSymbolIsType()
        {
            var controller = new OmnisharpController(CreateTestWorkspace(), null);
            var response = await controller.GotoDefinition(new Request
            {
                FileName = "bar.cs",
                Line = 9,
                Column = 25
            }) as ObjectResult;

            var definitionResponse = response.Value as GotoDefinitionResponse;

            Assert.Null(definitionResponse.FileName);
            Assert.Equal(0, definitionResponse.Line);
            Assert.Equal(0, definitionResponse.Column);
            Assert.Contains("public class List<T> : IList<T>, ICollection<T>", definitionResponse.MetadataSource);
        }

        OmnisharpWorkspace CreateTestWorkspace()
        {
            var source1 = @"using System;

class Foo {
}";
            var source2 = @"using System;
using System.Collections.Generic;
using System.Linq;

class Bar {
    public void Baz() {
        Console.WriteLine(""Stuff"");

        var foo = new List<string>();
        foo.ToList();
    }
}";

            return TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                { "foo.cs", source1 }, { "bar.cs", source2}
            });
        }
    }
}
