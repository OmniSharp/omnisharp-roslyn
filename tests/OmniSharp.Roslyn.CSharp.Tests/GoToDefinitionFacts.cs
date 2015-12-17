using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
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

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                { "foo.cs", source1 }, { "bar.cs", source2}
            });
            var controller = new GotoDefinitionService(workspace);
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 2,
                Column = 14,
                Timeout = 60000
            });

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

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                { "foo.cs", source1 }, { "bar.cs", source2}
            });
            var controller = new GotoDefinitionService(workspace);
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 2,
                Column = 14,
                Timeout = 60000
            });

            Assert.Null(definitionResponse.FileName);
            Assert.Equal(0, definitionResponse.Line);
            Assert.Equal(0, definitionResponse.Column);
        }

        [Fact]
        public async Task ReturnsPositionInMetadata_WhenSymbolIsMethod()
        {
            var controller = new GotoDefinitionService(await CreateTestWorkspace());
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 13,
                Column = 18,
                Timeout = 60000,
                WantMetadata = true
            });

            Assert.Null(definitionResponse.FileName);
            Assert.NotNull(definitionResponse.MetadataSource);
            Assert.Equal("mscorlib", definitionResponse.MetadataSource.AssemblyName);
            Assert.Equal("System.Guid", definitionResponse.MetadataSource.TypeName);
            // We probably shouldn't hard code metadata locations (they could change randomly)
            Assert.NotEqual(0, definitionResponse.Line);
            Assert.NotEqual(0, definitionResponse.Column);
        }

        [Fact]
        public async Task ReturnsPositionInMetadata_WhenSymbolIsExtensionMethod()
        {
            var controller = new GotoDefinitionService( await CreateTestWorkspace());
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 11,
                Column = 17,
                Timeout = 60000,
                WantMetadata = true
            });

            Assert.Null(definitionResponse.FileName);
            Assert.NotNull(definitionResponse.MetadataSource);
            Assert.Equal("mscorlib", definitionResponse.MetadataSource.AssemblyName);
            Assert.Equal("System.Collections.Generic.List`1", definitionResponse.MetadataSource.TypeName);
            Assert.NotEqual(0, definitionResponse.Line);
            Assert.NotEqual(0, definitionResponse.Column);
        }

        [Fact]
        public async Task ReturnsPositionInMetadata_WhenSymbolIsType()
        {
            var controller = new GotoDefinitionService( await CreateTestWorkspace());
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 9,
                Column = 25,
                Timeout = 60000,
                WantMetadata = true
            });

            Assert.Null(definitionResponse.FileName);
            Assert.NotNull(definitionResponse.MetadataSource);
            Assert.Equal("mscorlib", definitionResponse.MetadataSource.AssemblyName);
            Assert.Equal("System.Collections.Generic.List`1", definitionResponse.MetadataSource.TypeName);
            Assert.NotEqual(0, definitionResponse.Line);
            Assert.NotEqual(0, definitionResponse.Column);
        }

        [Fact]
        public async Task ReturnsPositionInMetadata_WhenSymbolIsGenericType()
        {
            var controller = new GotoDefinitionService(await CreateTestWorkspace());
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 12,
                Column = 26,
                Timeout = 60000,
                WantMetadata = true
            });

            Assert.Null(definitionResponse.FileName);
            Assert.NotNull(definitionResponse.MetadataSource);
            Assert.Equal("mscorlib", definitionResponse.MetadataSource.AssemblyName);
            Assert.Equal("System.Collections.Generic.Dictionary`2", definitionResponse.MetadataSource.TypeName);
            Assert.NotEqual(0, definitionResponse.Line);
            Assert.NotEqual(0, definitionResponse.Column);
        }

        [Fact]
        public async Task ReturnsFullNameInMetadata_WhenSymbolIsType()
        {
            var controller = new GotoDefinitionService(await CreateTestWorkspace());
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 10,
                Column = 23,
                Timeout = 60000,
                WantMetadata = true
            });

            Assert.Null(definitionResponse.FileName);
            Assert.NotNull(definitionResponse.MetadataSource);
            Assert.Equal("mscorlib", definitionResponse.MetadataSource.AssemblyName);
            Assert.Equal("System.String", definitionResponse.MetadataSource.TypeName);
            Assert.NotEqual(0, definitionResponse.Line);
            Assert.NotEqual(0, definitionResponse.Column);
        }

        async Task<OmnisharpWorkspace> CreateTestWorkspace()
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
        var str = String.Empty;
        foo.ToArray();
        var dict = new Dictionary<string, string>();
        Guid.NewGuid();
    }
}";

            return await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                { "foo.cs", source1 }, { "bar.cs", source2}
            });
        }
    }
}
