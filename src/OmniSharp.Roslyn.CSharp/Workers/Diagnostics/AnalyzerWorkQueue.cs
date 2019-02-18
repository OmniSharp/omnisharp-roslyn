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

        private readonly ConcurrentDictionary<DocumentId, (DateTime modified, Document document, CancellationTokenSource workDoneSource)> _workQueue =
            new ConcurrentDictionary<DocumentId, (DateTime modified, Document document, CancellationTokenSource workDoneSource)>();

        private readonly ConcurrentDictionary<DocumentId, (DateTime modified, Document document,  CancellationTokenSource workDoneSource)> _currentWork =
            new ConcurrentDictionary<DocumentId, (DateTime modified, Document document, CancellationTokenSource workDoneSource)>();
        private readonly Func<DateTime> _utcNow;
        private readonly int _timeoutForPendingWorkMs;
        private readonly ILogger<AnalyzerWorkQueue> _logger;

        public AnalyzerWorkQueue(ILoggerFactory loggerFactory, Func<DateTime> utcNow = null, int timeoutForPendingWorkMs = 15*1000)
        {
            if(utcNow == null)
                utcNow = () => DateTime.UtcNow;

            _logger = loggerFactory.CreateLogger<AnalyzerWorkQueue>();
            _utcNow = utcNow;
            _timeoutForPendingWorkMs = timeoutForPendingWorkMs;
        }

        public void PutWork(Document document)
        {
            _workQueue.AddOrUpdate(document.Id,
                (modified: DateTime.UtcNow, document, new CancellationTokenSource()),
                (_, oldValue) => (modified: DateTime.UtcNow, document, oldValue.workDoneSource));
        }

        public ImmutableArray<Document> TakeWork()
        {
            lock (_workQueue)
            {
                var now = _utcNow();
                var currentWork = _workQueue
                    .Where(x => ThrottlingPeriodNotActive(x.Value.modified, now))
                    .ToImmutableArray();

                foreach (var work in currentWork)
                {
                    _workQueue.TryRemove(work.Key, out _);
                    _currentWork.TryAdd(work.Key, work.Value);
                }

                return currentWork.Select(x => x.Value.document).ToImmutableArray();
            }
        }

        private bool ThrottlingPeriodNotActive(DateTime modified, DateTime now)
        {
            return (now - modified).TotalMilliseconds >= _throttlingMs;
        }

        public void MarkWorkAsCompleteForDocument(Document document)
        {
            if(_currentWork.TryGetValue(document.Id, out var work))
            {
                work.workDoneSource.Cancel();
                _currentWork.TryRemove(document.Id, out _);
            }
        }

        // Omnisharp V2 api expects that it can request current information of diagnostics any time,
        // however analysis is worker based and is eventually ready. This method is used to make api look
        // like it's syncronous even that actual analysis may take a while.
        public async Task WaitWorkReadyForDocuments(ImmutableArray<Document> documents)
        {
            var currentWorkMatches = _currentWork.Where(x => documents.Any(doc => doc.Id == x.Key));

            var pendingWorkThatDoesntExistInCurrentWork = _workQueue
                .Where(x => documents.Any(doc => doc.Id == x.Key))
                .Where(x => !currentWorkMatches.Any(currentWork => currentWork.Key == x.Key));

            await Task.WhenAll(
                currentWorkMatches.Concat(pendingWorkThatDoesntExistInCurrentWork)
                    .Select(x => Task.Delay(_timeoutForPendingWorkMs, x.Value.workDoneSource.Token)
                        .ContinueWith(task => LogTimeouts(task, x.Value.document.Name)))
                    .ToImmutableArray());
        }

        // This logs wait's for document diagnostics that continue without getting current version from analyzer.
        // This happens on larger solutions during initial load or situations where analysis slows down remarkably.
        private void LogTimeouts(Task task, string description)
        {
            if (!task.IsCanceled) _logger.LogDebug($"Timeout before work got ready for {description}.");
        }
    }

}
