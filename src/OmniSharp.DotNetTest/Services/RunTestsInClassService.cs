using System.Collections.Generic;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Eventing;
using OmniSharp.Mef;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.RunAllTestsInClass, LanguageNames.CSharp)]
    internal class RunTestsInClassService : BaseTestService<RunTestsInClassRequest, RunTestResponse[]>
    {
        [ImportingConstructor]
        public RunTestsInClassService(OmniSharpWorkspace workspace, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
        }

        protected override RunTestResponse[] HandleRequest(RunTestsInClassRequest request, TestManager testManager)
        {
            List<RunTestResponse> responses = new List<RunTestResponse>();
            if (testManager.IsConnected)
            {
                foreach (var methodName in request.MethodNamesInClass)
                    responses.Add(testManager.RunTest(methodName, request.TestFrameworkName, request.TargetFrameworkVersion));
            }
            else
            {
                var response = new RunTestResponse
                {
                    Failure = "Failed to connect to 'dotnet test' process",
                    Pass = false
                };
                responses.Add(response);
            }

            return responses.ToArray();
        }
    }
}
