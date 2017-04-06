using System;
using System.Composition;
using OmniSharp.DotNetTest.Models;

namespace OmniSharp.DotNetTest
{
    [Export, Shared]
    public class TestSessionManager
    {
        private TestManager _manager;

        public void StartSession(TestManager manager)
        {
            if (_manager != null)
            {
                throw new InvalidOperationException("Session already started.");
            }

            _manager = manager;
        }

        public void EndSession()
        {
            if (_manager == null)
            {
                throw new InvalidOperationException("Session not started.");
            }

            _manager = null;
        }

        public DebugDotNetTestStartResponse StartDebug(string methodName, string testFrameworkName)
        {
            if (_manager == null)
            {
                throw new InvalidOperationException("Session not started.");
            }

            return _manager.StartDebug(methodName, testFrameworkName);
        }

        public void DebugReady()
        {
            if (_manager == null)
            {
                throw new InvalidOperationException("Session not started.");
            }

            _manager.DebugReady();
        }
    }
}
