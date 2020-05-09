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
    [OmniSharpHandler(OmniSharpEndpoints.V2.RunTest, LanguageNames.CSharp)]
    internal class RunTestService : BaseTestService<RunTestRequest, RunTestResponse>
    {
        [ImportingConstructor]
        public RunTestService(OmniSharpWorkspace workspace, IDotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
        }

        protected override Task<RunTestResponse> HandleRequest(RunTestRequest request, TestManager testManager)
        {
            if (testManager.IsConnected)
            {
                return testManager.RunTestAsync(request.MethodName, request.RunSettings, request.TestFrameworkName, request.TargetFrameworkVersion, CancellationToken.None);
            }

            var response = new RunTestResponse
            {
                Failure = "Failed to connect to 'dotnet test' process",
                Pass = false,
                ContextHadNoTests = false
            };

            return Task.FromResult(response);
        }
    }
}
