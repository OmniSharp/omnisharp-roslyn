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

        private readonly ConcurrentDictionary<ProjectId, (DateTime modified, ProjectId projectId, CancellationTokenSource workDoneSource)> _workQueue =
            new ConcurrentDictionary<ProjectId, (DateTime modified, ProjectId projectId, CancellationTokenSource workDoneSource)>();

        private readonly ConcurrentDictionary<ProjectId, (DateTime modified, ProjectId projectId, CancellationTokenSource workDoneSource)> _currentWork = 
            new ConcurrentDictionary<ProjectId, (DateTime modified, ProjectId projectId, CancellationTokenSource workDoneSource)>();
        private readonly int _timeoutForPendingWorkMs;
        private ILogger<AnalyzerWorkQueue> _logger;

        public AnalyzerWorkQueue(ILoggerFactory loggerFactory, int throttleWorkMs = 300, int timeoutForPendingWorkMs = 60*1000)
        {
            _logger = loggerFactory.CreateLogger<AnalyzerWorkQueue>();
            _throttlingMs = throttleWorkMs;
            _timeoutForPendingWorkMs = timeoutForPendingWorkMs;
        }

        public void PutWork(ProjectId projectId)
        {
            _workQueue.AddOrUpdate(projectId,
                (modified: DateTime.UtcNow, projectId: projectId, new CancellationTokenSource()),
                (_, oldValue) => (modified: DateTime.UtcNow, projectId: projectId, workDoneSource: oldValue.workDoneSource));
        }

        public ImmutableArray<ProjectId> TakeWork()
        {
            lock (_workQueue)
            {
                var currentWork = _workQueue
                    .Where(x => x.Value.modified.AddMilliseconds(_throttlingMs) <= DateTime.UtcNow)
                    .OrderByDescending(x => x.Value.modified) // If you currently edit project X you want it will be highest priority and contains always latest possible analysis.
                    .Take(1) // Limit mount of work executed by once. This is needed on large solution...
                    .ToImmutableArray();

                foreach (var work in currentWork)
                {
                    _workQueue.TryRemove(work.Key, out _);
                    _currentWork.TryAdd(work.Key, work.Value);
                }

                return currentWork.Select(x => x.Key).ToImmutableArray();
            }
        }

        public void AckWorkAsDone(ProjectId projectId)
        {
            if(_currentWork.TryGetValue(projectId, out var work))
            {
                work.workDoneSource.Cancel();
                _currentWork.TryRemove(projectId, out _);
            }
        }

        // Omnisharp V2 api expects that it can request current information of diagnostics any time,
        // however analysis is worker based and is eventually ready. This method is used to make api look
        // like it's syncronous even that actual analysis may take a while.
        public async Task WaitForPendingWork(ImmutableArray<ProjectId> projectIds)
        {
            var currentWorkMatches = _currentWork.Where(x => projectIds.Any(pid => pid == x.Key));

            var pendingWorkThatDoesntExistInCurrentWork = _workQueue
                .Where(x => projectIds.Any(pid => pid == x.Key))
                .Where(x => !currentWorkMatches.Any(currentWork => currentWork.Key == x.Key));

            await Task.WhenAll(
                currentWorkMatches.Concat(pendingWorkThatDoesntExistInCurrentWork)
                    .Select(x => Task.Delay(_timeoutForPendingWorkMs, x.Value.workDoneSource.Token)
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
