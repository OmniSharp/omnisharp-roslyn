using System;
using System.IO;
using System.Threading.Tasks;
using OmniSharp.Cake.Services.RequestHandlers.Navigation;
using OmniSharp.Models;
using OmniSharp.Models.GotoDefinition;
using OmniSharp.Models.UpdateBuffer;
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
                Assert.Equal(2, response.Line);
                Assert.Equal(22, response.Column);
            }
        }
    }
}
