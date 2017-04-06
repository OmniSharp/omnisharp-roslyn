using System.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Mef;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [Shared]
    [OmniSharpHandler(OmnisharpEndpoints.V2.DebugTestCheck, LanguageNames.CSharp)]
    [OmniSharpHandler(OmnisharpEndpoints.V2.DebugTestReady, LanguageNames.CSharp)]
    [OmniSharpHandler(OmnisharpEndpoints.V2.DebugTestStart, LanguageNames.CSharp)]
    public class DebugTestService : BaseTestService,
        RequestHandler<DebugTestCheckRequest, DebugTestCheckResponse>,
        RequestHandler<DebugTestReadyRequest, DebugTestReadyResponse>,
        RequestHandler<DebugTestStartRequest, DebugTestStartResponse>
    {
        private DebugSessionManager _debugSessionManager;

        [ImportingConstructor]
        public DebugTestService(DebugSessionManager debugSessionManager, OmniSharpWorkspace workspace, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
            _debugSessionManager = debugSessionManager;
        }

        public Task<DebugTestCheckResponse> Handle(DebugTestCheckRequest request)
        {
            var document = Workspace.GetDocument(request.FileName);
            var workingDirectory = Path.GetDirectoryName(document.Project.FilePath);

            var debugType = DotNetCli.IsLegacy(workingDirectory)
                ? "launch"
                : "attach";

            var response = new DebugTestCheckResponse
            {
                DebugType = debugType
            };

            return Task.FromResult(response);
        }

        public Task<DebugTestReadyResponse> Handle(DebugTestReadyRequest request)
        {
            var response = _debugSessionManager.DebugReady();

            return Task.FromResult(response);
        }

        public Task<DebugTestStartResponse> Handle(DebugTestStartRequest request)
        {
            var testManager = CreateTestManager(request.FileName);
            _debugSessionManager.StartSession(testManager);

            var response = _debugSessionManager.DebugStart(request.MethodName, request.TestFrameworkName);

            return Task.FromResult(response);
        }
    }
}
