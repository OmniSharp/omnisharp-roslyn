using System;
using System.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNetTest.Models;
using OmniSharp.Utilities;

namespace OmniSharp.DotNetTest
{
    [Export, Shared]
    internal class DebugSessionManager
    {
        private readonly object _gate = new object();
        private readonly ILogger _logger;

        private TestManager _testManager;
        private CancellationTokenSource _tokenSource;

        [ImportingConstructor]
        public DebugSessionManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DebugSessionManager>();
        }

        private void VerifySession(bool isStarted)
        {
            lock (_gate)
            {
                if ((_testManager != null) != isStarted)
                {
                    if (isStarted)
                    {
                        _logger.LogError("Debug session not started.");
                        throw new InvalidOperationException("Debug session not started.");
                    }
                    else
                    {
                        _logger.LogError("Debug session not started.");
                        throw new InvalidOperationException("Debug session already started.");
                    }
                }
            }
        }

        public void StartSession(TestManager testManager)
        {
            lock (_gate)
            {
                VerifySession(isStarted: false);

                _testManager = testManager;
                _tokenSource = new CancellationTokenSource();

                _logger.LogInformation("Debug session started.");
            }
        }

        public void EndSession()
        {
            lock (_gate)
            {
                if (_tokenSource == null && _testManager == null)
                {
                    return;
                }

                _tokenSource.Cancel();
                _tokenSource.Dispose();
                _tokenSource = null;

                _testManager.Dispose();
                _testManager = null;

                _logger.LogInformation("Debug session ended.");
            }
        }

        public Task<DebugTestGetStartInfoResponse> DebugGetStartInfoAsync(string methodName, string runSettings, string testFrameworkName, string targetFrameworkVersion, CancellationToken cancellationToken)
            => DebugGetStartInfoAsync(new string[] { methodName }, runSettings, testFrameworkName, targetFrameworkVersion, cancellationToken);
        
        public Task<DebugTestGetStartInfoResponse> DebugGetStartInfoAsync(string[] methodNames, string runSettings, string testFrameworkName, string targetFrameworkVersion, CancellationToken cancellationToken)
        {
            VerifySession(isStarted: true);

            return _testManager.DebugGetStartInfoAsync(methodNames, runSettings, testFrameworkName, targetFrameworkVersion, cancellationToken);
        }

        public async Task<DebugTestLaunchResponse> DebugLaunchAsync(int targetProcessId)
        {
            VerifySession(isStarted: true);

            var process = Process.GetProcessById(targetProcessId);

            process.EnableRaisingEvents = true;
            process.OnExit(() =>
            {
                EndSession();
            });

            await _testManager.DebugLaunchAsync(_tokenSource.Token);

            return new DebugTestLaunchResponse();
        }

        internal Task<DebugTestStopResponse> DebugStopAsync()
        {
            EndSession();

            return Task.FromResult(new DebugTestStopResponse());
        }
    }
}
