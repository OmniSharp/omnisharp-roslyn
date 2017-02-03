using System;
using System.Collections.Concurrent;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Utilities;

namespace OmniSharp.Services
{
    [Export, Shared]
    public class DotNetCliService
    {
        private readonly ILogger _logger;
        private readonly IEventEmitter _eventEmitter;
        private readonly ConcurrentDictionary<string, object> _locks;
        private readonly SemaphoreSlim _semaphore;

        [ImportingConstructor]
        public DotNetCliService(ILoggerFactory loggerFactory, IEventEmitter eventEmitter)
        {
            this._logger = loggerFactory.CreateLogger<DotNetCliService>();
            this._eventEmitter = eventEmitter;
            this._locks = new ConcurrentDictionary<string, object>();
            this._semaphore = new SemaphoreSlim(Environment.ProcessorCount / 2);
        }

        public void Restore(string projectPath, Action onFailure)
        {
            Task.Factory.StartNew(() =>
            {
                _logger.LogInformation($"Begin restoring project {projectPath}");

                var restoreLock = _locks.GetOrAdd(projectPath, new object());
                lock (restoreLock)
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
                        _locks.TryRemove(projectPath, out removedLock);

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
