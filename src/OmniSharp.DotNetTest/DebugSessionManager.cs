using System;
using System.Composition;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Utilities;

namespace OmniSharp.DotNetTest
{
    [Export, Shared]
    public class DebugSessionManager
    {
        private readonly ILogger _logger;
        private TestManager _testManager;
        private Process _testProcess;

        public bool IsSessionStarted => _testManager != null;
        public bool IsTestProcessRunning => _testProcess != null && !_testProcess.HasExited;

        [ImportingConstructor]
        public DebugSessionManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DebugSessionManager>();
        }

        public void StartSession(TestManager testManager)
        {
            if (IsSessionStarted)
            {
                throw new InvalidOperationException("Debug session already started.");
            }

            _testManager = testManager;
            _logger.LogInformation("Debug session started.");
        }

        public void EndSession()
        {
            if (!IsSessionStarted)
            {
                throw new InvalidOperationException("Debug session not started.");
            }

            _testProcess = null;
            _testManager = null;

            _logger.LogInformation("Debug session ended.");
        }

        public DebugTestStartResponse DebugStart(string methodName, string testFrameworkName)
        {
            if (!IsSessionStarted)
            {
                throw new InvalidOperationException("Debug session not started.");
            }

            _testProcess = _testManager.DebugStart(methodName, testFrameworkName);

            _testProcess.EnableRaisingEvents = true;
            _testProcess.OnExit(() =>
            {
                EndSession();
            });

            return new DebugTestStartResponse
            {
                HostProcessId = Process.GetCurrentProcess().Id,
                ProcessId = _testProcess.Id
            };
        }

        public DebugTestReadyResponse DebugReady()
        {
            if (!IsSessionStarted)
            {
                throw new InvalidOperationException("Debug session not started.");
            }

            if (!IsTestProcessRunning)
            {
                throw new InvalidOperationException("Test process is not running.");
            }

            _testManager.DebugReady();

            return new DebugTestReadyResponse
            {
                IsReady = true
            };
        }
    }
}
