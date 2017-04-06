using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Mef;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmnisharpEndpoints.V2.RunTest, LanguageNames.CSharp)]
    public class RunTestService : BaseTestService<RunTestRequest, RunTestResponse>
    {
        [ImportingConstructor]
        public RunTestService(OmniSharpWorkspace workspace, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
        }

        protected override RunTestResponse HandleRequest(RunTestRequest request, TestManager testManager)
        {
            return testManager.IsConnected
                ? testManager.RunTest(request.MethodName, request.TestFrameworkName)
                : new RunTestResponse { Failure = "Failed to connect to 'dotnet test' process", Pass = false };
        }
    }
}
