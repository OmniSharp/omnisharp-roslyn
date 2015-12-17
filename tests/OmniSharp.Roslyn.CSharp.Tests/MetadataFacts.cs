using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
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

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                { "foo.cs", source1 }, { "bar.cs", source2}
            });
            var controller = new MetadataService(workspace);
            var response = await controller.Handle(new MetadataRequest
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

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                { "foo.cs", source1 }, { "bar.cs", source2}
            });
            var controller = new MetadataService(workspace);
            var response = await controller.Handle(new MetadataRequest
            {
#if DNXCORE50
                AssemblyName = "System.Linq",
#else
                AssemblyName = "System.Core",
#endif
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

            var workspace = await TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                { "foo.cs", source1 }, { "bar.cs", source2}
            });
            var controller = new MetadataService(workspace);
            var response = await controller.Handle(new MetadataRequest
            {
                AssemblyName = "mscorlib",
                TypeName = "System.Collections.Generic.List`1",
                Timeout = 60000
            });

            Assert.NotNull(response.Source);

            response = await controller.Handle(new MetadataRequest
            {
                AssemblyName = "mscorlib",
                TypeName = "System.Collections.Generic.Dictionary`2"
            });

            Assert.NotNull(response.Source);
        }
    }
}
