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
            using var testManager = TestManager.Start(document.Project, DotNetCli, EventEmitter, LoggerFactory);

            if (testManager.IsConnected)
            {
                return await testManager.RunTestsInContextAsync(request.Line, request.Column, document, request.RunSettings, request.TargetFrameworkVersion, CancellationToken.None);
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
