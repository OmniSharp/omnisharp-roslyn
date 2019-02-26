using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        private readonly ConcurrentDictionary<DocumentId, (DateTime modified, CancellationTokenSource workDoneSource)> _workQueue =
            new ConcurrentDictionary<DocumentId, (DateTime modified, CancellationTokenSource workDoneSource)>();

        private readonly ConcurrentDictionary<DocumentId, (DateTime modified,  CancellationTokenSource workDoneSource)> _currentWork =
            new ConcurrentDictionary<DocumentId, (DateTime modified, CancellationTokenSource workDoneSource)>();

        private readonly Func<DateTime> _utcNow;
        private readonly int _maximumDelayWhenWaitingForResults;
        private readonly ILogger<AnalyzerWorkQueue> _logger;

        public AnalyzerWorkQueue(ILoggerFactory loggerFactory, Func<DateTime> utcNow = null, int timeoutForPendingWorkMs = 15*1000)
        {
            utcNow = utcNow ?? (() => DateTime.UtcNow);
            _logger = loggerFactory.CreateLogger<AnalyzerWorkQueue>();
            _utcNow = utcNow;
            _maximumDelayWhenWaitingForResults = timeoutForPendingWorkMs;
        }

        public void PutWork(DocumentId documentId)
        {
            _workQueue.AddOrUpdate(documentId,
                (modified: DateTime.UtcNow, new CancellationTokenSource()),
                (_, oldValue) => (modified: DateTime.UtcNow, oldValue.workDoneSource));
        }

        public ImmutableArray<DocumentId> TakeWork()
        {
            lock (_workQueue)
            {
                var now = _utcNow();
                var currentWork = _workQueue
                    .Where(x => ThrottlingPeriodNotActive(x.Value.modified, now))
                    .OrderByDescending(x => x.Value.modified)
                    .Take(50)
                    .ToImmutableArray();

                foreach (var work in currentWork)
                {
                    _workQueue.TryRemove(work.Key, out _);
                    _currentWork.TryAdd(work.Key, work.Value);
                }

                return currentWork.Select(x => x.Key).ToImmutableArray();
            }
        }

        private bool ThrottlingPeriodNotActive(DateTime modified, DateTime now)
        {
            return (now - modified).TotalMilliseconds >= _throttlingMs;
        }

        public void MarkWorkAsCompleteForDocumentId(DocumentId documentId)
        {
            if(_currentWork.TryGetValue(documentId, out var work))
            {
                work.workDoneSource.Cancel();
                _currentWork.TryRemove(documentId, out _);
            }
        }

        // Omnisharp V2 api expects that it can request current information of diagnostics any time,
        // however analysis is worker based and is eventually ready. This method is used to make api look
        // like it's syncronous even that actual analysis may take a while.
        public async Task WaitForResultsAsync(ImmutableArray<DocumentId> documentIds)
        {
            var items = new List<(DateTime modified, CancellationTokenSource workDoneSource)>();

            foreach (var documentId in documentIds)
            {
                if (_currentWork.ContainsKey(documentId))
                {
                    items.Add(_currentWork[documentId]);
                }
                else if (_workQueue.ContainsKey(documentId))
                {
                    items.Add(_workQueue[documentId]);
                }
            }

            await Task.WhenAll(items.Select(item =>
                                Task.Delay(_maximumDelayWhenWaitingForResults, item.workDoneSource.Token)
                                .ContinueWith(task => LogTimeouts(task, documentIds))));
        }

        // This logs wait's for documentId diagnostics that continue without getting current version from analyzer.
        // This happens on larger solutions during initial load or situations where analysis slows down remarkably.
        private void LogTimeouts(Task task, IEnumerable<DocumentId> documentIds)
        {
            if (!task.IsCanceled) _logger.LogDebug($"Timeout before work got ready for one of documents {string.Join(",", documentIds)}.");
        }
    }

}
