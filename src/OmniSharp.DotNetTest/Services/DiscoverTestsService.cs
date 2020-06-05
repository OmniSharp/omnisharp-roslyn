using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Eventing;
using OmniSharp.Mef;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.DiscoverTests, LanguageNames.CSharp)]
    internal class DiscoverTestsService : BaseTestService<DiscoverTestsRequest, DiscoverTestsResponse>
    {
        [ImportingConstructor]
        public DiscoverTestsService(OmniSharpWorkspace workspace, IDotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
        }

        protected override async Task<DiscoverTestsResponse> HandleRequest(DiscoverTestsRequest request, TestManager testManager)
        {
            if (testManager.IsConnected)
            {
                return await testManager.DiscoverTestsAsync(request.RunSettings, request.TestFrameworkName, request.TargetFrameworkVersion, default(CancellationToken));
            }
            
            throw new InvalidOperationException("The debugger could not be started");
        }
    }
}
