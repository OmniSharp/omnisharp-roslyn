using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Mef;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmnisharpEndpoints.V2.DebugDotNetTestStart, LanguageNames.CSharp)]
    public class DebugDotNetTestStartService : BaseTestService<DebugDotNetTestStartRequest, DebugDotNetTestStartResponse>
    {
        private TestSessionManager _testSessionManager;

        [ImportingConstructor]
        public DebugDotNetTestStartService(TestSessionManager testSessionManager, OmniSharpWorkspace workspace, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory)
            : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
            _testSessionManager = testSessionManager;
        }

        protected override DebugDotNetTestStartResponse HandleRequest(DebugDotNetTestStartRequest request)
        {
            var testManager = CreateTestManager(request.FileName);
            _testSessionManager.StartSession(testManager);

            return _testSessionManager.StartDebug(request.MethodName, request.TestFrameworkName);
        }
    }
}
