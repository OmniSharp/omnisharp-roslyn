#nullable enable

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
    [OmniSharpHandler(OmniSharpEndpoints.V2.RunTestsInContext, LanguageNames.CSharp)]
    internal class RunTestsInContextService : BaseTestService, IRequestHandler<RunTestsInContextRequest, RunTestResponse>
    {
        [ImportingConstructor]
        public RunTestsInContextService(OmniSharpWorkspace workspace, IDotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
        }

        public async Task<RunTestResponse> Handle(RunTestsInContextRequest request)
        {
            var document = Workspace.GetDocument(request.FileName);
            if (document is null)
            {
                return new RunTestResponse
                {
                    Failure = "File is not part of a C# project in the loaded solution.",
                    Pass = false,
                    ContextHadNoTests = true
                };
            }

            using var testManager = TestManager.Create(document.Project, DotNetCli, EventEmitter, LoggerFactory);

            var (methodNames, testFramework) = await testManager.GetContextTestMethodNames(request.Line, request.Column, document, CancellationToken.None);

            if (methodNames is null)
            {
                return new RunTestResponse
                {
                    Pass = false,
                    Failure = "Could not find any tests to run",
                    ContextHadNoTests = true
                };
            }

            testManager.Connect(request.NoBuild);

            if (testManager.IsConnected)
            {
                return await testManager.RunTestAsync(methodNames, request.RunSettings, testFramework, request.TargetFrameworkVersion, CancellationToken.None);
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
