using OmniSharp.Cake.Services.RequestHandlers.Navigation;
using OmniSharp.Models.V2.GotoDefinition;

using System.IO;
using System.Linq;
using System.Threading.Tasks;

using TestUtility;

using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public sealed class GotoDefinitionV2Facts : CakeSingleRequestHandlerTestFixture<GotoDefinitionV2Handler>
    {
        public GotoDefinitionV2Facts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.V2.GotoDefinition;

        [Fact]
        public async Task ShouldSupportLoadedFiles()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");

                var request = new GotoDefinitionRequest
                {
                    FileName = fileName,
                    Line = 8,
                    Column = 10
                };

                var requestHandler = GetRequestHandler(host);
                var response = await requestHandler.Handle(request);

                Assert.NotNull(response.Definitions);
                Assert.Single(response.Definitions);
                var definition = response.Definitions.Single();

                Assert.Equal(Path.Combine(testProject.Directory, "foo.cake"), definition.Location.FileName);
                Assert.Equal(4, definition.Location.Range.Start.Line);
                Assert.Equal(22, definition.Location.Range.Start.Column);
            }
        }

        [Fact]
        public async Task ShouldNavigateToAProperty()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");

                var request = new GotoDefinitionRequest
                {
                    FileName = fileName,
                    Line = 11,
                    Column = 20
                };

                var requestHandler = GetRequestHandler(host);
                var response = await requestHandler.Handle(request);

                Assert.NotNull(response.Definitions);
                Assert.Single(response.Definitions);
                var definition = response.Definitions.Single();

                Assert.Equal(Path.Combine(testProject.Directory, "foo.cake"), definition.Location.FileName);
                Assert.Equal(0, definition.Location.Range.Start.Line);
                Assert.Equal(4, definition.Location.Range.Start.Column);
            }
        }

        [Fact]
        public async Task ShouldNavigateIntoDslMetadataWithoutGenericParams()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");

                var request = new GotoDefinitionRequest
                {
                    FileName = fileName,
                    Line = 11,
                    Column = 10,
                    WantMetadata = true
                };

                var requestHandler = GetRequestHandler(host);
                var response = await requestHandler.Handle(request);

                Assert.NotNull(response.Definitions);
                Assert.Single(response.Definitions);
                var definition = response.Definitions.Single();
                Assert.StartsWith("$metadata$", definition.Location.FileName);

                var metadata = definition.MetadataSource;
                Assert.NotNull(metadata);
                Assert.Equal("Cake.Common", metadata.AssemblyName);
                Assert.Equal("Cake.Common.Diagnostics.LoggingAliases", metadata.TypeName);
            }
        }

        [Fact]
        public async Task ShouldNavigateIntoDslMetadataWithGenericParams()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");

                var request = new GotoDefinitionRequest
                {
                    FileName = fileName,
                    Line = 0,
                    Column = 16,
                    WantMetadata = true
                };

                var requestHandler = GetRequestHandler(host);
                var response = await requestHandler.Handle(request);

                Assert.NotNull(response.Definitions);
                Assert.Single(response.Definitions);
                var definition = response.Definitions.Single();
                Assert.StartsWith("$metadata$", definition.Location.FileName);

                var metadata = definition.MetadataSource;
                Assert.NotNull(metadata);
                Assert.Equal("Cake.Common", metadata.AssemblyName);
                Assert.Equal("Cake.Common.ArgumentAliases", metadata.TypeName);
            }
        }

        [Fact]
        public async Task ShouldNavigateIntoDslMetadataProperty()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");

                var request = new GotoDefinitionRequest
                {
                    FileName = fileName,
                    Line = 12,
                    Column = 37,
                    WantMetadata = true
                };

                var requestHandler = GetRequestHandler(host);
                var response = await requestHandler.Handle(request);

                Assert.NotNull(response.Definitions);
                Assert.Single(response.Definitions);
                var definition = response.Definitions.Single();
                Assert.StartsWith("$metadata$", definition.Location.FileName);

                var metadata = definition.MetadataSource;
                Assert.NotNull(metadata);
                Assert.Equal("Cake.Common", metadata.AssemblyName);
                Assert.Equal("Cake.Common.Build.BuildSystemAliases", metadata.TypeName);
            }
        }

        [Fact]
        public async Task ShouldFindMultipleLocationsForPartial()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("CakeProject", shadowCopy: false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var fileName = Path.Combine(testProject.Directory, "build.cake");

                var request = new GotoDefinitionRequest
                {
                    FileName = fileName,
                    Line = 7,
                    Column = 5
                };

                var requestHandler = GetRequestHandler(host);
                var response = await requestHandler.Handle(request);

                Assert.NotNull(response.Definitions);
                var expectedFile = Path.Combine(testProject.Directory, "foo.cake");
                Assert.Collection(
                    response.Definitions,
                    d =>
                    {
                        Assert.Equal(expectedFile, d.Location.FileName);
                        Assert.Equal(2, d.Location.Range.Start.Line);
                        Assert.Equal(21, d.Location.Range.Start.Column);
                    },
                    d =>
                    {
                        Assert.Equal(expectedFile, d.Location.FileName);
                        Assert.Equal(15, d.Location.Range.Start.Line);
                        Assert.Equal(21, d.Location.Range.Start.Column);
                    });
            }
        }
    }
}
