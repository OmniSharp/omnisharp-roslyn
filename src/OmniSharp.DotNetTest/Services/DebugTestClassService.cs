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
    [OmniSharpHandler(OmniSharpEndpoints.V2.DebugTestsInClassGetStartInfo, LanguageNames.CSharp)]
    class DebugTestClassService : BaseTestService,
        IRequestHandler<DebugTestClassGetStartInfoRequest, DebugTestGetStartInfoResponse>
    {
        private DebugSessionManager _debugSessionManager;

        [ImportingConstructor]
        public DebugTestClassService(DebugSessionManager debugSessionManager, OmniSharpWorkspace workspace, IDotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
            _debugSessionManager = debugSessionManager;
        }

        public async Task<DebugTestGetStartInfoResponse> Handle(DebugTestClassGetStartInfoRequest request)
        {
            var testManager = CreateTestManager(request.FileName);
            _debugSessionManager.StartSession(testManager);

            return await _debugSessionManager.DebugGetStartInfoAsync(request.MethodNames, request.RunSettings, request.TestFrameworkName, request.TargetFrameworkVersion, CancellationToken.None);
        }
    }
}
