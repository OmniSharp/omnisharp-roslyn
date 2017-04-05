using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Mef;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmnisharpEndpoints.V2.RunDotNetTest, LanguageNames.CSharp)]
    public class RunDotNetTestService : BaseTestService<RunDotNetTestRequest, RunDotNetTestResponse>
    {
        [ImportingConstructor]
        public RunDotNetTestService(OmniSharpWorkspace workspace, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
        }

        protected override RunDotNetTestResponse HandleRequest(RunDotNetTestRequest request, TestManager testManager)
        {
            return testManager.IsConnected
                ? testManager.RunTest(request.MethodName, request.TestFrameworkName)
                : new RunDotNetTestResponse { Failure = "Failed to connect to 'dotnet test' process", Pass = false };
        }
    }
}
