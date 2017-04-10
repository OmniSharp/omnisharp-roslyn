using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Mef;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [Shared]
    [OmniSharpHandler(OmnisharpEndpoints.V2.DebugTestGetStartInfo, LanguageNames.CSharp)]
    [OmniSharpHandler(OmnisharpEndpoints.V2.DebugTestRun, LanguageNames.CSharp)]
    public class DebugTestService : BaseTestService,
        RequestHandler<DebugTestGetStartInfoRequest, DebugTestGetStartInfoResponse>,
        RequestHandler<DebugTestRunRequest, DebugTestRunResponse>
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

            var response = _debugSessionManager.DebugGetStartInfo(request.MethodName, request.TestFrameworkName);

            return Task.FromResult(response);
        }

        public Task<DebugTestRunResponse> Handle(DebugTestRunRequest request)
        {
            var response = _debugSessionManager.DebugRun();

            return Task.FromResult(response);
        }

    }
}
