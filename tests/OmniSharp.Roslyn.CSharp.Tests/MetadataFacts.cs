using System.Threading.Tasks;
using OmniSharp.Models;
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

        [Fact]
        public async Task ReturnsSource_ForSpecialType()
        {
            var assemblyName = AssemblyHelpers.CorLibName;
            var typeName = "System.String";

            await TestMetadataAsync(assemblyName, typeName);
        }

        [Fact]
        public async Task ReturnsSource_ForNormalType()
        {
#if NETCOREAPP1_1
            var assemblyName = "System.Linq";
#else
            var assemblyName = "System.Core";
#endif

            var typeName = "System.Linq.Enumerable";

            await TestMetadataAsync(assemblyName, typeName);
        }

        [Fact]
        public async Task ReturnsSource_ForGenericType()
        {
            var assemblyName = AssemblyHelpers.CorLibName;
            var typeName = "System.Collections.Generic.List`1";

            await TestMetadataAsync(assemblyName, typeName);
        }

        private async Task TestMetadataAsync(string assemblyName, string typeName)
        {
            var testFile = new TestFile("dummy.cs", "class C {}");

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
