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

        public Task<DebugTestGetStartInfoResponse> Handle(DebugTestsInContextGetStartInfoRequest request)
        {
            var testManager = CreateTestManager(request.FileName);
            if (testManager.IsConnected)
            {
                _debugSessionManager.StartSession(testManager);
                return _debugSessionManager.DebugGetStartInfoAsync(
                    request.Line, request.Column, Workspace.GetDocument(request.FileName),
                    request.RunSettings, request.TargetFrameworkVersion,
                    CancellationToken.None);
            }

            throw new InvalidOperationException("The debugger could not be started");
        }
    }
}
