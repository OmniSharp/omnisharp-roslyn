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

        private readonly ConcurrentDictionary<DocumentId, (DateTime modified, Document document, ManualResetEvent manualResetEvent)> _workQueue =
            new ConcurrentDictionary<DocumentId, (DateTime modified, Document document, ManualResetEvent manualResetEvent)>();

        private readonly ConcurrentDictionary<DocumentId, (DateTime modified, Document document, ManualResetEvent manualResetEvent)> _currentWork =
            new ConcurrentDictionary<DocumentId, (DateTime modified, Document document, ManualResetEvent manualResetEvent)>();
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
                (modified: DateTime.UtcNow, document, new ManualResetEvent(false)),
                (_, oldValue) => (modified: DateTime.UtcNow, document, oldValue.manualResetEvent));
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
                _currentWork.TryRemove(document.Id, out _);
                work.manualResetEvent.Set();
            }
        }

        // Omnisharp V2 api expects that it can request current information of diagnostics any time,
        // however analysis is worker based and is eventually ready. This method is used to make api look
        // like it's syncronous even that actual analysis may take a while.
        public Task WaitForPendingWorkDoneEvent(ImmutableArray<Document> documents)
        {
            return Task.Run(() =>
            {
                var currentWorkMatches = _currentWork.Where(x => documents.Any(doc => doc.Id == x.Key));

                var pendingWorkThatDoesntExistInCurrentWork = _workQueue
                    .Where(x => documents.Any(doc => doc.Id == x.Key))
                    .Where(x => !currentWorkMatches.Any(currentWork => currentWork.Key == x.Key));

                // Not perfect but WaitAll only accepts up to 64 handles at once.
                var workToWait = currentWorkMatches.Concat(pendingWorkThatDoesntExistInCurrentWork).Take(60);

                if (workToWait.Any())
                {
                    var waitComplete = WaitHandle.WaitAll(
                        workToWait.Select(x => x.Value.manualResetEvent).ToArray(),
                        _timeoutForPendingWorkMs);

                    if (!waitComplete)
                    {
                        _logger.LogError($"Timeout before work got ready. Documents waited {String.Join(",", workToWait.Select(x => x.Value.document.Name))}.");
                    }
                }
            });
        }
    }
}
