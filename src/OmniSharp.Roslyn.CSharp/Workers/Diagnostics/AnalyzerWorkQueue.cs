using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    public class AnalyzerWorkQueue
    {
        private readonly int _throttlingMs = 300;

        private readonly ConcurrentDictionary<ProjectId, (DateTime modified, ProjectId projectId)> _workQueue =
            new ConcurrentDictionary<ProjectId, (DateTime modified, ProjectId projectId)>();

        private readonly ConcurrentDictionary<ProjectId, CancellationTokenSource> _blockingWork = new ConcurrentDictionary<ProjectId, CancellationTokenSource>();
        private ILogger<AnalyzerWorkQueue> _logger;

        public AnalyzerWorkQueue(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<AnalyzerWorkQueue>();
        }

        public void PushWork(ProjectId projectId)
        {
            _workQueue.AddOrUpdate(projectId,
                (modified: DateTime.UtcNow, projectId: projectId),
                (_, oldValue) => (modified: DateTime.UtcNow, projectId: projectId));
        }

        public ImmutableArray<ProjectId> PopWork()
        {
            lock (_workQueue)
            {
                var currentWork = _workQueue
                    .Where(x => x.Value.modified.AddMilliseconds(_throttlingMs) < DateTime.UtcNow)
                    .OrderByDescending(x => x.Value.modified) // If you currently edit project X you want it will be highest priority and contains always latest possible analysis.
                    .Take(1) // Limit mount of work executed by once. This is needed on large solution...
                    .ToImmutableArray();

                foreach (var workKey in currentWork.Select(x => x.Key))
                {
                    _workQueue.TryRemove(workKey, out _);
                    _blockingWork.TryAdd(workKey, new CancellationTokenSource());
                }

                return currentWork.Select(x => x.Key).ToImmutableArray();
            }
        }

        public void AckWork(ProjectId projectId)
        {
            if(_blockingWork.TryGetValue(projectId, out var tokenSource))
            {
                tokenSource.Cancel();
                _blockingWork.TryRemove(projectId, out _);
            }
        }

        // Omnisharp V2 api expects that it can request current information of diagnostics any time,
        // however analysis is worker based and is eventually ready. This method is used to make api look
        // like it's syncronous even that actual analysis may take a while.
        public async Task WaitForPendingWork(ImmutableArray<ProjectId> projectIds)
        {
            await Task.WhenAll(_blockingWork
                .Where(x => projectIds.Any(pid => pid == x.Key))
                .Select(x => Task.Delay(30 * 1000, x.Value.Token)
                    .ContinueWith(task => LogTimeouts(task, x.Key.ToString())))
                .ToImmutableArray());
        }

        // This is basically asserting mechanism for hanging analysis if any. If this doesn't exist tracking
        // down why results doesn't come up (for example in situation when theres bad analyzer that takes ages to complete).
        private void LogTimeouts(Task task, string description)
        {
            if (!task.IsCanceled) _logger.LogError($"Timeout before work got ready for {description}.");
        }
    }
}