using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Services.RequestHandlers.Navigation;
using OmniSharp.Models;
using OmniSharp.Models.FindSymbols;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Cake.Tests
{
    public class FindSymbolsFacts : CakeSingleRequestHandlerTestFixture<FindSymbolsHandler>
    {
        private readonly ILogger _logger;

        public FindSymbolsFacts(ITestOutputHelper testOutput) : base(testOutput)
        {
            _logger = LoggerFactory.CreateLogger<FindSymbolsFacts>();
        }

        protected override string EndpointName => OmniSharpEndpoints.FindSymbols;

        [Fact]
        public async Task ShouldFindSymbolsInCakeProjects()
        {
            var symbols = await FindSymbols("CakeProject");
            Assert.NotEmpty(symbols.QuickFixes);
        }

        [Fact]
        public async Task ShouldNotFindSymbolsInCSharpProjects()
        {
            var symbols = await FindSymbols("ProjectAndSolution");
            Assert.Empty(symbols.QuickFixes);
        }

        private async Task<QuickFixResponse> FindSymbols(string projectName)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName, shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var request = new FindSymbolsRequest
                {
                    Filter = ""
                };

                var requestHandler = GetRequestHandler(host);

                return await requestHandler.Handle(request);
            }
        }
    }
}
