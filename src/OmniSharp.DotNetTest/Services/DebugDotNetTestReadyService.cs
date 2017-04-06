using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Mef;
using OmniSharp.Services;

namespace OmniSharp.DotNetTest.Services
{
    [OmniSharpHandler(OmnisharpEndpoints.V2.DebugDotNetTestReady, LanguageNames.CSharp)]
    public class DebugDotNetTestReadyService : BaseTestService<DebugDotNetTestReadyRequest, DebugDotNetTestReadyResponse>
    {
        private TestSessionManager _testSessionManager;

        [ImportingConstructor]
        public DebugDotNetTestReadyService(TestSessionManager testSessionManager, OmniSharpWorkspace workspace, DotNetCliService dotNetCli, IEventEmitter eventEmitter, ILoggerFactory loggerFactory) : base(workspace, dotNetCli, eventEmitter, loggerFactory)
        {
            _testSessionManager = testSessionManager;
        }

        protected override DebugDotNetTestReadyResponse HandleRequest(DebugDotNetTestReadyRequest request)
        {
            _testSessionManager.DebugReady();
            return new DebugDotNetTestReadyResponse();
        }
    }
}
