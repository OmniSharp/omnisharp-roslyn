using System.Threading.Tasks;
using OmniSharp.Models.Metadata;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class MetadataFacts : AbstractSingleRequestHandlerTestFixture<MetadataService>
    {
        public MetadataFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.Metadata;

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ReturnsSource_ForSpecialType(string filename)
        {
            var assemblyName = AssemblyHelpers.CorLibName;
            var typeName = "System.String";

            await TestMetadataAsync(filename, assemblyName, typeName);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ReturnsSource_ForNormalType(string filename)
        {
            var assemblyName = "System.Core";
            var typeName = "System.Linq.Enumerable";

            await TestMetadataAsync(filename, assemblyName, typeName);
        }

        [Theory]
        [InlineData("dummy.cs")]
        [InlineData("dummy.csx")]
        public async Task ReturnsSource_ForGenericType(string filename)
        {
            var assemblyName = AssemblyHelpers.CorLibName;
            var typeName = "System.Collections.Generic.List`1";

            await TestMetadataAsync(filename, assemblyName, typeName);
        }

        private async Task TestMetadataAsync(string filename, string assemblyName, string typeName)
        {
            var testFile = new TestFile(filename, "class C {}");

            using (var host = CreateOmniSharpHost(testFile))
            {
                var requestHandler = GetRequestHandler(host);

                var request = new MetadataRequest
                {
                    AssemblyName = assemblyName,
                    TypeName = typeName,
                    Timeout = 60000
                };

                var response = await requestHandler.Handle(request);

                Assert.NotNull(response.Source);
            }
        }
    }
}
