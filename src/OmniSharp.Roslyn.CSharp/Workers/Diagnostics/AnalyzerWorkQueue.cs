using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    public class AnalyzerWorkQueue
    {
        private class Queue
        {
            public Queue(TimeSpan throttling)
            {
                Throttling = throttling;
            }

            public ImmutableHashSet<DocumentId> WorkWaitingToExecute { get; set; } = ImmutableHashSet<DocumentId>.Empty;
            public ImmutableHashSet<DocumentId> WorkExecuting { get; set; } = ImmutableHashSet<DocumentId>.Empty;
            public DateTime LastThrottlingBegan { get; set; } = DateTime.UtcNow;
            public TimeSpan Throttling { get; }
            public CancellationTokenSource WorkPendingToken { get; set; }
        }

        private readonly Dictionary<AnalyzerWorkType, Queue> _queues = null;

        private readonly ILogger<AnalyzerWorkQueue> _logger;
        private readonly Func<DateTime> _utcNow;
        private readonly int _maximumDelayWhenWaitingForResults;
        private readonly object _queueLock = new object();

        public AnalyzerWorkQueue(ILoggerFactory loggerFactory, int timeoutForPendingWorkMs, Func<DateTime> utcNow = null)
        {
            _queues = new Dictionary<AnalyzerWorkType, Queue>
            {
                { AnalyzerWorkType.Foreground, new Queue(TimeSpan.FromMilliseconds(150)) },
                { AnalyzerWorkType.Background, new Queue(TimeSpan.FromMilliseconds(1500)) }
            };

            _logger = loggerFactory.CreateLogger<AnalyzerWorkQueue>();
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _maximumDelayWhenWaitingForResults = timeoutForPendingWorkMs;
        }

        public void PutWork(IReadOnlyCollection<DocumentId> documentIds, AnalyzerWorkType workType)
        {
            lock (_queueLock)
            {
                var queue = _queues[workType];

                if (queue.WorkWaitingToExecute.IsEmpty)
                    queue.LastThrottlingBegan = _utcNow();

                if (queue.WorkPendingToken == null)
                    queue.WorkPendingToken = new CancellationTokenSource();

                queue.WorkWaitingToExecute = queue.WorkWaitingToExecute.Union(documentIds);
            }
        }

        public IReadOnlyCollection<DocumentId> TakeWork(AnalyzerWorkType workType)
        {
            lock (_queueLock)
            {
                var queue = _queues[workType];

                if (IsThrottlingActive(queue) || queue.WorkWaitingToExecute.IsEmpty)
                    return ImmutableHashSet<DocumentId>.Empty;

                queue.WorkExecuting = queue.WorkWaitingToExecute;
                queue.WorkWaitingToExecute = ImmutableHashSet<DocumentId>.Empty;
                return queue.WorkExecuting;
            }
        }

        private bool IsThrottlingActive(Queue queue)
        {
            return (_utcNow() - queue.LastThrottlingBegan).TotalMilliseconds <= queue.Throttling.TotalMilliseconds;
        }

        public void WorkComplete(AnalyzerWorkType workType)
        {
            lock (_queueLock)
            {
                if(_queues[workType].WorkExecuting.IsEmpty)
                    return;

                _queues[workType].WorkPendingToken?.Cancel();
                _queues[workType].WorkPendingToken = null;
                _queues[workType].WorkExecuting = ImmutableHashSet<DocumentId>.Empty;
            }
        }

        // Omnisharp V2 api expects that it can request current information of diagnostics any time (single file/current document),
        // however analysis is worker based and is eventually ready. This method is used to make api look
        // like it's syncronous even that actual analysis may take a while.
        public Task WaitForegroundWorkComplete()
        {
            var queue = _queues[AnalyzerWorkType.Foreground];

            if (queue.WorkPendingToken == null || (queue.WorkPendingToken == null && queue.WorkWaitingToExecute.IsEmpty))
                return Task.CompletedTask;

            return Task.Delay(_maximumDelayWhenWaitingForResults, queue.WorkPendingToken.Token)
                .ContinueWith(task => LogTimeouts(task));
        }

        public bool TryPromote(DocumentId id)
        {
            if (_queues[AnalyzerWorkType.Background].WorkWaitingToExecute.Contains(id) || _queues[AnalyzerWorkType.Background].WorkExecuting.Contains(id))
            {
                PutWork(new[] { id }, AnalyzerWorkType.Foreground);
                return true;
            }

            return false;
        }

        private void LogTimeouts(Task task)
        {
            if (!task.IsCanceled) _logger.LogWarning($"Timeout before work got ready for foreground analysis queue. This is assertion to prevent complete api hang in case of error.");
        }
    }
}
