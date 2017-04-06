using System;
using System.Composition;
using System.Diagnostics;
using OmniSharp.DotNetTest.Models;

namespace OmniSharp.DotNetTest
{
    [Export, Shared]
    public class DebugSessionManager
    {
        private TestManager _testManager;
        private Process _testProcess;

        public void StartSession(TestManager testManager)
        {
            if (_testManager != null)
            {
                throw new InvalidOperationException("Debug session already started.");
            }

            _testManager = testManager;
        }

        public void EndSession()
        {
            if (_testManager == null)
            {
                throw new InvalidOperationException("Debug session not started.");
            }

            _testManager = null;
        }

        public DebugTestStartResponse DebugStart(string methodName, string testFrameworkName)
        {
            if (_testManager == null)
            {
                throw new InvalidOperationException("Debug session not started.");
            }

            _testProcess = _testManager.DebugStart(methodName, testFrameworkName);

            return new DebugTestStartResponse
            {
                HostProcessId = Process.GetCurrentProcess().Id,
                ProcessId = _testProcess.Id
            };
        }

        public DebugTestReadyResponse DebugReady()
        {
            if (_testManager == null)
            {
                throw new InvalidOperationException("Debug session not started.");
            }

            if (_testProcess == null ||
                _testProcess.HasExited)
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
