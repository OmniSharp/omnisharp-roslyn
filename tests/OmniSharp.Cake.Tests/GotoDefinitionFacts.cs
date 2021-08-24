using System.IO;
using System.Threading.Tasks;
using OmniSharp.Cake.Services.RequestHandlers.Navigation;
using OmniSharp.Models.GotoDefinition;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public sealed class GotoDefinitionFacts : CakeSingleRequestHandlerTestFixture<GotoDefinitionHandler>
    {
        public GotoDefinitionFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        protected override string EndpointName => OmniSharpEndpoints.GotoDefinition;

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

                Assert.Equal(Path.Combine(testProject.Directory, "foo.cake"), response.FileName);
                Assert.Equal(4, response.Line);
                Assert.Equal(22, response.Column);
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

                Assert.Equal(Path.Combine(testProject.Directory, "foo.cake"), response.FileName);
                Assert.Equal(0, response.Line);
                Assert.Equal(4, response.Column);
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

                Assert.StartsWith("$metadata$", response.FileName);
                var metadata = response.MetadataSource;
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

                Assert.StartsWith("$metadata$", response.FileName);
                var metadata = response.MetadataSource;
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

                Assert.StartsWith("$metadata$", response.FileName);
                var metadata = response.MetadataSource;
                Assert.NotNull(metadata);
                Assert.Equal("Cake.Common", metadata.AssemblyName);
                Assert.Equal("Cake.Common.Build.BuildSystemAliases", metadata.TypeName);
            }
        }
    }
}
