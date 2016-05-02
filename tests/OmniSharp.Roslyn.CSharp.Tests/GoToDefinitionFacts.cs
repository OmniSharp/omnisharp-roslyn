using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using OmniSharp.Services;
using OmniSharp.Tests;
using TestUtility.Annotate;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class GoToDefinitionFacts
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly IOmnisharpAssemblyLoader _loader;

        public GoToDefinitionFacts()
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddConsole();
            _logger = _loggerFactory.CreateLogger<GoToDefinitionFacts>();

            _loader = new AnnotateAssemblyLoader(_logger);
        }

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
            var controller = new GotoDefinitionService(workspace, CreateMetadataHelper());
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 1,
                Column = 13,
                Timeout = 60000
            });

            Assert.Equal("foo.cs", definitionResponse.FileName);
            Assert.Equal(2, definitionResponse.Line);
            Assert.Equal(6, definitionResponse.Column);
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
            var controller = new GotoDefinitionService(workspace, CreateMetadataHelper());
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 1,
                Column = 13,
                Timeout = 60000
            });

            Assert.Null(definitionResponse.FileName);
            Assert.Equal(0, definitionResponse.Line);
            Assert.Equal(0, definitionResponse.Column);
        }

        [Fact]
        public async Task ReturnsPositionInMetadata_WhenSymbolIsMethod()
        {
            var controller = new GotoDefinitionService(await CreateTestWorkspace(), CreateMetadataHelper());
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 12,
                Column = 17,
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
            var controller = new GotoDefinitionService(await CreateTestWorkspace(), CreateMetadataHelper());
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 10,
                Column = 16,
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
            var controller = new GotoDefinitionService(await CreateTestWorkspace(), CreateMetadataHelper());
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 8,
                Column = 24,
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
            var controller = new GotoDefinitionService(await CreateTestWorkspace(), CreateMetadataHelper());
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 11,
                Column = 25,
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
            var controller = new GotoDefinitionService(await CreateTestWorkspace(), CreateMetadataHelper());
            RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> requestHandler = controller;
            var definitionResponse = await requestHandler.Handle(new GotoDefinitionRequest
            {
                FileName = "bar.cs",
                Line = 9,
                Column = 22,
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

        private MetadataHelper CreateMetadataHelper()
        {
            return new MetadataHelper(_loader);
        }

        private async Task<OmnisharpWorkspace> CreateTestWorkspace()
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
