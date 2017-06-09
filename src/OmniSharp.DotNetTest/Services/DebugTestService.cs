﻿using System.Composition;
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
    [OmniSharpHandler(OmniSharpEndpoints.V2.DebugTestGetStartInfo, LanguageNames.CSharp)]
    [OmniSharpHandler(OmniSharpEndpoints.V2.DebugTestLaunch, LanguageNames.CSharp)]
    [OmniSharpHandler(OmniSharpEndpoints.V2.DebugTestStop, LanguageNames.CSharp)]
    internal class DebugTestService : BaseTestService,
        IRequestHandler<DebugTestGetStartInfoRequest, DebugTestGetStartInfoResponse>,
        IRequestHandler<DebugTestLaunchRequest, DebugTestLaunchResponse>,
        IRequestHandler<DebugTestStopRequest, DebugTestStopResponse>
    {
        private DebugSessionManager _debugSessionManager;

        [ImportingConstructor]
        public DebugTestService(DebugSessionManager debugSessionManager, OmniSharpWorkspace workspace, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
            _debugSessionManager = debugSessionManager;
        }

        public Task<DebugTestGetStartInfoResponse> Handle(DebugTestGetStartInfoRequest request)
        {
            var testManager = CreateTestManager(request.FileName);
            _debugSessionManager.StartSession(testManager);

            return _debugSessionManager.DebugGetStartInfoAsync(request.MethodName, request.TestFrameworkName, CancellationToken.None);
        }

        public Task<DebugTestLaunchResponse> Handle(DebugTestLaunchRequest request)
        {
            return _debugSessionManager.DebugLaunchAsync(request.TargetProcessId);
        }

        public Task<DebugTestStopResponse> Handle(DebugTestStopRequest request)
        {
            return _debugSessionManager.DebugStopAsync();
        }
    }
}
