using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.DotNet.Tools
{
    public class PackagesRestoreTool
    {
        private readonly ILogger _logger;
        private readonly IEventEmitter _eventEmitter;
        private readonly ConcurrentDictionary<string, object> _projectLocks;
        private readonly SemaphoreSlim _semaphore;

        public PackagesRestoreTool(ILoggerFactory logger, IEventEmitter emitter)
        {
            _logger = logger.CreateLogger<PackagesRestoreTool>();
            _eventEmitter = emitter;

            _projectLocks = new ConcurrentDictionary<string, object>();
            _semaphore = new SemaphoreSlim(Environment.ProcessorCount / 2);
        }

        public void Restore(string projectPath, Action onFailure)
        {
            Task.Factory.StartNew(() =>
            {
                _logger.LogInformation($"Begin restoring project {projectPath}");

                var projectLock = _projectLocks.GetOrAdd(projectPath, new object());
                lock (projectLock)
                {
                    var exitStatus = new ProcessExitStatus(-1);
                    _eventEmitter.RestoreStarted(projectPath);
                    _semaphore.Wait();
                    try
                    {
                        // A successful restore will update the project lock file which is monitored
                        // by the dotnet project system which eventually update the Roslyn model
                        exitStatus = ProcessHelper.Run("dotnet", "restore", projectPath);
                    }
                    finally
                    {
                        _semaphore.Release();

                        object removedLock;
                        _projectLocks.TryRemove(projectPath, out removedLock);

                        _eventEmitter.RestoreFinished(projectPath, exitStatus.Succeeded);

                        if (exitStatus.Failed)
                        {
                            onFailure();
                        }

                        _logger.LogInformation($"Finish restoring project {projectPath}. Exit code {exitStatus}");
                    }
                }
            });
        }
    }
}
