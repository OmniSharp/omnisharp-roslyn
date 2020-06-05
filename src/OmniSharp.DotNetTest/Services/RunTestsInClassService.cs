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
    [OmniSharpHandler(OmniSharpEndpoints.V2.RunAllTestsInClass, LanguageNames.CSharp)]
    internal class RunTestsInClassService : BaseTestService<RunTestsInClassRequest, RunTestResponse>
    {
        [ImportingConstructor]
        public RunTestsInClassService(OmniSharpWorkspace workspace, IDotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
        }

        protected override async Task<RunTestResponse> HandleRequest(RunTestsInClassRequest request, TestManager testManager)
        {
            if (testManager.IsConnected)
            {
                return await testManager.RunTestAsync(request.MethodNames, request.RunSettings, request.TestFrameworkName, request.TargetFrameworkVersion, CancellationToken.None);
            }

            var response = new RunTestResponse
            {
                Failure = "Failed to connect to 'dotnet test' process",
                Pass = false,
                ContextHadNoTests = false
            };

            return response;
        }
    }
}
