using System.Linq;
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
            var symbols = await FindSymbols("CakeProject", minFilterLength: null, maxItemsToReturn: null);
            Assert.NotEmpty(symbols.QuickFixes);
        }

        [Fact]
        public async Task ShouldNotFindSymbolsInCakeProjectsDueToEmptyFilter()
        {
            var symbols = await FindSymbols("CakeProject", minFilterLength: 1, maxItemsToReturn: 0);
            Assert.Empty(symbols.QuickFixes);
        }

        [Fact]
        public async Task ShouldFindLimitedNumberOfSymbolsInCakeProjects()
        {
            var symbols = await FindSymbols("CakeProject", minFilterLength: 0, maxItemsToReturn: 100);
            Assert.Equal(100, symbols.QuickFixes.Count());
        }

        [Fact]
        public async Task ShouldNotFindSymbolsInCSharpProjects()
        {
            var symbols = await FindSymbols("ProjectAndSolution", minFilterLength: 0, maxItemsToReturn: 0);
            Assert.Empty(symbols.QuickFixes);
        }

        private async Task<QuickFixResponse> FindSymbols(string projectName, int? minFilterLength, int? maxItemsToReturn)
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName, shadowCopy : false))
            using (var host = CreateOmniSharpHost(testProject.Directory))
            {
                var request = new FindSymbolsRequest
                {
                    Filter = "",
                    MinFilterLength = minFilterLength,
                    MaxItemsToReturn = maxItemsToReturn
                };

                var requestHandler = GetRequestHandler(host);

                return await requestHandler.Handle(request);
            }
        }
    }
}
