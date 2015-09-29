using System.Collections.Generic;
using System.Threading.Tasks;
using OmniSharp.Models;
using Xunit;

namespace OmniSharp.Tests
{
    public class MetadataFacts
    {
        [Fact]
        public async Task ReturnsSource_ForSpecialType()
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
            var response = await controller.Metadata(new MetadataRequest
            {
                AssemblyName = "mscorlib",
                TypeName = "System.String",
                Timeout = 60000
            });

            Assert.NotNull(response.Source);
        }

        [Fact]
        public async Task ReturnsSource_ForNormalType()
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
            var response = await controller.Metadata(new MetadataRequest
            {
                AssemblyName = "System.Core",
                TypeName = "System.Linq.Enumerable",
                Timeout = 60000
            });

            Assert.NotNull(response.Source);
        }

        [Fact]
        public async Task ReturnsSource_ForGenericType()
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
            var response = await controller.Metadata(new MetadataRequest
            {
                AssemblyName = "mscorlib",
                TypeName = "System.Collections.Generic.List`1",
                Timeout = 60000
            });

            Assert.NotNull(response.Source);

            response = await controller.Metadata(new MetadataRequest
            {
                AssemblyName = "mscorlib",
                TypeName = "System.Collections.Generic.Dictionary`2"
            });

            Assert.NotNull(response.Source);
        }
    }
}
