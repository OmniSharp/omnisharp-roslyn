using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

#nullable enable

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    public class AsyncAnalyzerWorkQueue
    {
        private readonly object _lock = new();
        private readonly Queue _foreground = new();
        private readonly Queue _background = new();
        private readonly ILogger<AnalyzerWorkQueue> _logger;
        private TaskCompletionSource<object?> _takeWorkWaiter = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AsyncAnalyzerWorkQueue(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<AnalyzerWorkQueue>();
        }

        public int PendingCount
        {
            get
            {
                lock (_lock)
                    return _foreground.PendingCount + _background.PendingCount;
            }
        }

        public void PutWork(IReadOnlyCollection<DocumentId> documentIds, AnalyzerWorkType workType)
        {
            lock (_lock)
            {
                foreach (var documentId in documentIds)
                {
                    _foreground.RequestCancellationIfActive(documentId);
                    _background.RequestCancellationIfActive(documentId);

                    if (workType == AnalyzerWorkType.Foreground)
                        _foreground.Enqueue(documentId);
                    else if (workType == AnalyzerWorkType.Background)
                        _background.Enqueue(documentId);
                }

                // Complete the work waiter task to allow work to be taken from the queue.
                if (!_takeWorkWaiter.Task.IsCompleted)
                    _takeWorkWaiter.SetResult(null);
            }
        }

        public async Task<QueueItem> TakeWorkAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Task awaitTask;

                lock (_lock)
                {
                    if (_foreground.TryDequeue(out var documentId, out var cancellationTokenSource))
                    {
                        return new QueueItem
                        (
                            DocumentId: documentId,
                            CancellationToken: cancellationTokenSource.Token,
                            AnalyzerWorkType: AnalyzerWorkType.Foreground,
                            DocumentCount: _foreground.MaximumPendingCount,
                            DocumentCountRemaining: _foreground.PendingCount
                        );
                    }
                    else if (_background.TryDequeue(out documentId, out cancellationTokenSource))
                    {
                        return new QueueItem
                        (
                            DocumentId: documentId,
                            CancellationToken: cancellationTokenSource.Token,
                            AnalyzerWorkType: AnalyzerWorkType.Background,
                            DocumentCount: _background.MaximumPendingCount,
                            DocumentCountRemaining: _background.PendingCount
                        );
                    }

                    if (_foreground.PendingCount == 0 && _background.PendingCount == 0 && _takeWorkWaiter.Task.IsCompleted)
                        _takeWorkWaiter = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

                    awaitTask = _takeWorkWaiter.Task;
                }

                // There is no chance of the default cancellation token being cancelled, so we can
                // simply wait for work to be queued. Otherwise, we need to handle the case that the
                // token is cancelled before we have work to return.
                if (cancellationToken == default)
                {
                    await awaitTask.ConfigureAwait(false);
                }
                else
                {
                    var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

                    using (cancellationToken.Register(() => tcs.SetResult(null)))
                    {
                        await Task.WhenAny(awaitTask, tcs.Task).ConfigureAwait(false);
                    }
                }
            }
        }

        public void WorkComplete(QueueItem item)
        {
            lock (_lock)
            {
                if (item.AnalyzerWorkType == AnalyzerWorkType.Foreground)
                    _foreground.WorkComplete(item.DocumentId, item.CancellationToken);
                else if (item.AnalyzerWorkType == AnalyzerWorkType.Background)
                    _background.WorkComplete(item.DocumentId, item.CancellationToken);
            }
        }

        public async Task WaitForegroundWorkComplete(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            Task waitForgroundTask;

            lock (_lock)
                waitForgroundTask = _foreground.GetWaiter();

            if (waitForgroundTask.IsCompleted)
                return;

            if (cancellationToken == default)
            {
                await waitForgroundTask.ConfigureAwait(false); 

                return;
            }

            var taskCompletion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (cancellationToken.Register(() => taskCompletion.SetResult(null)))
            {
                await Task.WhenAny(taskCompletion.Task, waitForgroundTask).ConfigureAwait(false);

                if (!waitForgroundTask.IsCompleted)
                    _logger.LogWarning($"Timeout before work got ready for foreground analysis queue. This is assertion to prevent complete api hang in case of error.");
            }
        }

        public bool TryPromote(DocumentId id)
        {
            var shouldEnqueue = false;

            lock (_lock)
            {
                shouldEnqueue = _background.IsEnqueued(id) || _background.IsActive(id);
            }

            if (shouldEnqueue)
                PutWork(new[] { id }, AnalyzerWorkType.Foreground);

            return shouldEnqueue;
        }

        public record QueueItem
        (
            DocumentId DocumentId,
            CancellationToken CancellationToken,
            AnalyzerWorkType AnalyzerWorkType,
            int DocumentCount,
            int DocumentCountRemaining
        );

        private class Queue
        {
            private readonly HashSet<DocumentId> _pendingHash = new();
            private readonly Queue<DocumentId> _pendingQueue = new();
            private readonly Dictionary<DocumentId, List<CancellationTokenSource>> _active = new();
            private readonly List<(HashSet<DocumentId> DocumentIds, TaskCompletionSource<object?> TaskCompletionSource)> _waiters = new();

            public int PendingCount => _pendingQueue.Count;

            public int ActiveCount => _active.Count;

            public int MaximumPendingCount { get; private set; }

            public void RequestCancellationIfActive(DocumentId documentId)
            {
                if (_active.TryGetValue(documentId, out var active))
                {
                    foreach (var cts in active)
                        cts.Cancel();
                }
            }

            public void Enqueue(DocumentId documentId)
            {
                if (_pendingHash.Add(documentId))
                {
                    _pendingQueue.Enqueue(documentId);

                    if (_pendingQueue.Count > MaximumPendingCount)
                        MaximumPendingCount = _pendingQueue.Count;
                }
            }

            public bool IsEnqueued(DocumentId documentId) =>
                _pendingHash.Contains(documentId);

            public bool IsActive(DocumentId documentId) =>
                _active.ContainsKey(documentId);

            public void Remove(DocumentId documentId)
            {
                if (_pendingHash.Contains(documentId))
                {
                    _pendingHash.Remove(documentId);

                    var backgroundQueueItems = _pendingQueue.ToList();

                    _pendingQueue.Clear();

                    foreach (var item in backgroundQueueItems)
                    {
                        if (item != documentId)
                            _pendingQueue.Enqueue(item);
                    }
                }
            }
            
            public bool TryDequeue([NotNullWhen(true)] out DocumentId? documentId, [NotNullWhen(true)] out CancellationTokenSource? cancellationTokenSource)
            {
                if (_pendingQueue.Count > 0)
                {
                    documentId = _pendingQueue.Dequeue();

                    _pendingHash.Remove(documentId);

                    if (!_active.TryGetValue(documentId, out var cancellationTokenSources))
                        _active[documentId] = cancellationTokenSources = new List<CancellationTokenSource>();

                    cancellationTokenSource = new CancellationTokenSource();

                    cancellationTokenSources.Add(cancellationTokenSource);

                    return true;
                }

                documentId = null;
                cancellationTokenSource = null;

                return false;
            }

            public void WorkComplete(DocumentId documentId, CancellationToken cancellationToken)
            {
                if (_active.TryGetValue(documentId, out var cancellationTokenSources))
                {
                    foreach (var cancellationTokenSource in cancellationTokenSources.ToList())
                    {
                        if (cancellationTokenSource.Token == cancellationToken)
                        {
                            cancellationTokenSource.Dispose();

                            cancellationTokenSources.Remove(cancellationTokenSource);

                            break;
                        }
                    }

                    if (cancellationTokenSources.Count == 0)
                        _active.Remove(documentId);

                    var isReenqueued = cancellationToken.IsCancellationRequested
                        && (_pendingHash.Contains(documentId) || _active.ContainsKey(documentId));

                    if (!isReenqueued)
                    {
                        foreach (var waiter in _waiters.ToList())
                        {
                            if (waiter.DocumentIds.Remove(documentId) && waiter.DocumentIds.Count == 0)
                            {
                                waiter.TaskCompletionSource.SetResult(null);

                                _waiters.Remove(waiter);
                            }
                        }
                    }
                }
            }

            public Task GetWaiter()
            {
                if (_active.Count == 0 && _pendingQueue.Count == 0)
                    return Task.CompletedTask;

                var documentIds = new HashSet<DocumentId>(_pendingHash.Concat(_active.Keys));

                var waiter = _waiters.FirstOrDefault(x => x.DocumentIds.SetEquals(documentIds));

                if (waiter == default)
                {
                    waiter = (documentIds, new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously));

                    _waiters.Add(waiter);
                }

                return waiter.TaskCompletionSource.Task;
            }
        }
    }
}
