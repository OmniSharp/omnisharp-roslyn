using System;
using System.Composition;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;

namespace OmniSharp.DotNetTest
{
    [Export, Shared]
    public class DebugSessionManager
    {
        private readonly ILogger _logger;
        private TestManager _testManager;

        [ImportingConstructor]
        public DebugSessionManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DebugSessionManager>();
        }

        private void VerifySession(bool isStarted)
        {
            if (_testManager != null != isStarted)
            {
                if (isStarted)
                {
                    throw new InvalidOperationException("Debug session not started.");
                }
                else
                {
                    throw new InvalidOperationException("Debug session already started.");
                }
            }
        }

        public void StartSession(TestManager testManager)
        {
            VerifySession(isStarted: false);

            _testManager = testManager;
            _logger.LogInformation("Debug session started.");
        }

        public void EndSession()
        {
            VerifySession(isStarted: true);

            _testManager.Dispose();
            _testManager = null;

            _logger.LogInformation("Debug session ended.");
        }

        public DebugTestGetStartInfoResponse DebugGetStartInfo(string methodName, string testFrameworkName)
        {
            VerifySession(isStarted: true);

            return _testManager.DebugGetStartInfo(methodName, testFrameworkName);
        }

        public DebugTestRunResponse DebugRun()
        {
            VerifySession(isStarted: true);

            _testManager.DebugRun();
            EndSession();

            return new DebugTestRunResponse();
        }
    }
}
