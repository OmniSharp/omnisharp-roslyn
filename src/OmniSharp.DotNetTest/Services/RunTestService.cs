using System.Composition;
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
        public RunTestService(OmniSharpWorkspace workspace, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
        }

        protected override RunTestResponse HandleRequest(RunTestRequest request, TestManager testManager)
        {
            if (testManager.IsConnected)
            {
                return testManager.RunTest(request.MethodName, request.TestFrameworkName);
            }

            var response = new RunTestResponse
            {
                Failure = "Failed to connect to 'dotnet test' process",
                Pass = false
            };

            return response;
        }
    }
}
