#nullable enable

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
    [Shared]
    [OmniSharpHandler(OmniSharpEndpoints.V2.DebugTestsInContextGetStartInfo, LanguageNames.CSharp)]
    internal class DebugTestsInContextService : BaseTestService,
        IRequestHandler<DebugTestsInContextGetStartInfoRequest, DebugTestGetStartInfoResponse>
    {
        private readonly DebugSessionManager _debugSessionManager;

        [ImportingConstructor]
        public DebugTestsInContextService(DebugSessionManager debugSessionManager, OmniSharpWorkspace workspace, IDotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
            _debugSessionManager = debugSessionManager;
        }

        public async Task<DebugTestGetStartInfoResponse> Handle(DebugTestsInContextGetStartInfoRequest request)
        {
            var document = Workspace.GetDocument(request.FileName);
            if (document is null)
            {
                return new DebugTestGetStartInfoResponse
                {
                    Succeeded = false,
                    FailureReason = "File is not part of a C# project in the loaded solution.",
                    ContextHadNoTests = true,
                };
            }

            var testManager = TestManager.Create(document.Project, DotNetCli, EventEmitter, LoggerFactory);

            var (methodNames, testFramework) = await testManager.GetContextTestMethodNames(request.Line, request.Column, document, CancellationToken.None);

            if (methodNames is null)
            {
                return new DebugTestGetStartInfoResponse
                {
                    Succeeded = false,
                    FailureReason = "Could not find any tests to run",
                    ContextHadNoTests = true,

                };
            }

            testManager.Connect(request.NoBuild);

            if (testManager.IsConnected)
            {
                _debugSessionManager.StartSession(testManager);
                return await _debugSessionManager.DebugGetStartInfoAsync(methodNames, request.RunSettings, testFramework, request.TargetFrameworkVersion, CancellationToken.None);
            }

            return new DebugTestGetStartInfoResponse
            {
                FailureReason = "Failed to connect to the 'dotnet test' process",
                Succeeded = false
            };
        }
    }
}
