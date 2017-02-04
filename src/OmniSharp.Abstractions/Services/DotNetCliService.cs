using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.IO;
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

        private static void RemoveMSBuildEnvironmentVariables(IDictionary<string, string> environment)
        {
            // Remove various MSBuild environment variables set by OmniSharp to ensure that
            // the .NET CLI is not launched with the wrong values.
            environment.Remove("MSBUILD_EXE_PATH");
            environment.Remove("MSBuildExtensionsPath");
            environment.Remove("MSBuildSDKsPath");
        }

        public void Restore(string projectFilePath, Action onFailure = null)
        {
            var projectDirectory = Path.GetDirectoryName(projectFilePath);

            Task.Factory.StartNew(() =>
            {
                _logger.LogInformation($"Begin restoring project {projectFilePath}");

                var restoreLock = _locks.GetOrAdd(projectFilePath, new object());
                lock (restoreLock)
                {
                    var exitStatus = new ProcessExitStatus(-1);
                    _eventEmitter.RestoreStarted(projectFilePath);
                    _semaphore.Wait();
                    try
                    {
                        // A successful restore will update the project lock file which is monitored
                        // by the dotnet project system which eventually update the Roslyn model
                        exitStatus = ProcessHelper.Run("dotnet", "restore", projectDirectory, updateEnvironment: RemoveMSBuildEnvironmentVariables);
                    }
                    finally
                    {
                        _semaphore.Release();

                        object removedLock;
                        _locks.TryRemove(projectFilePath, out removedLock);

                        _eventEmitter.RestoreFinished(projectFilePath, exitStatus.Succeeded);

                        if (exitStatus.Failed && onFailure != null)
                        {
                            onFailure();
                        }

                        _logger.LogInformation($"Finish restoring project {projectFilePath}. Exit code {exitStatus}");
                    }
                }
            });
        }
    }
}
