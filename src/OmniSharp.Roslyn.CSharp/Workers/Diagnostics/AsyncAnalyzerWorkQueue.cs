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
        private readonly Queue _forground = new();
        private readonly Queue _background = new();
        private readonly ILogger<AnalyzerWorkQueue> _logger;
        private TaskCompletionSource<object?> _takeWorkWaiter = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource<object?> _waitForgroundWaiter = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AsyncAnalyzerWorkQueue(ILoggerFactory loggerFactory)
        {
            _waitForgroundWaiter.SetResult(null);

            _logger = loggerFactory.CreateLogger<AnalyzerWorkQueue>();
        }

        public void PutWork(IReadOnlyCollection<DocumentId> documentIds, AnalyzerWorkType workType)
        {
            lock (_lock)
            {
                foreach (var documentId in documentIds)
                {
                    _forground.RequestCancellationIfActive(documentId);
                    _background.RequestCancellationIfActive(documentId);

                    if (workType == AnalyzerWorkType.Foreground)
                    {
                        if (_waitForgroundWaiter.Task.IsCompleted)
                            _waitForgroundWaiter = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

                        _forground.Enqueue(documentId);

                        _background.Remove(documentId);
                    }
                    else if (workType == AnalyzerWorkType.Background)
                    {
                        if (_forground.IsEnqueued(documentId))
                            continue;

                        _background.Enqueue(documentId);
                    }
                }

                if (!_takeWorkWaiter.Task.IsCompleted)
                    _takeWorkWaiter.SetResult(null);
            }
        }

        public async Task<QueueItem> TakeWorkAsync()
        {
            while (true)
            {
                Task awaitTask;

                lock (_lock)
                {
                    if (_forground.TryDequeue(out var documentId, out var cancellationTokenSource))
                    {
                        return new QueueItem
                        (
                            DocumentId: documentId,
                            CancellationToken: cancellationTokenSource.Token,
                            AnalyzerWorkType: AnalyzerWorkType.Foreground,
                            DocumentCount: _forground.MaximumPendingCount,
                            DocumentCountRemaining: _forground.PendingCount
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

                    if (_forground.PendingCount == 0 && _background.PendingCount == 0 && _takeWorkWaiter.Task.IsCompleted)
                        _takeWorkWaiter = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

                    awaitTask = _takeWorkWaiter.Task;
                }

                await awaitTask;
            }
        }

        public void WorkComplete(QueueItem item)
        {
            lock (_lock)
            {
                if (item.AnalyzerWorkType == AnalyzerWorkType.Foreground)
                {
                    _forground.WorkComplete(item.DocumentId, item.CancellationToken);

                    if (_forground.PendingCount == 0
                        && _forground.ActiveCount == 0
                        && !_waitForgroundWaiter.Task.IsCompleted)
                    {
                        _waitForgroundWaiter.SetResult(null);
                    }
                }
                else if (item.AnalyzerWorkType == AnalyzerWorkType.Background)
                {
                    _background.WorkComplete(item.DocumentId, item.CancellationToken);
                }
            }
        }

        public async Task WaitForegroundWorkComplete(CancellationToken cancellationToken = default)
        {
            var waitForgroundTask = _waitForgroundWaiter.Task;

            if (waitForgroundTask.IsCompleted || cancellationToken.IsCancellationRequested)
                return;

            if (cancellationToken == default)
            {
                await waitForgroundTask;

                return;
            }

            var taskCompletion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (cancellationToken.Register(() => taskCompletion.SetResult(null)))
            {
                await Task.WhenAny(taskCompletion.Task, waitForgroundTask);

                if (!waitForgroundTask.IsCompleted)
                    _logger.LogWarning($"Timeout before work got ready for foreground analysis queue. This is assertion to prevent complete api hang in case of error.");
            }
        }

        public bool TryPromote(DocumentId id)
        {
            var shouldEnqueue = false;

            lock (_lock)
            {
                shouldEnqueue = _forground.IsEnqueued(id) || _forground.IsActive(id);
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
            private readonly HashSet<DocumentId> _hash = new();
            private readonly Queue<DocumentId> _pending = new();
            private readonly Dictionary<DocumentId, List<CancellationTokenSource>> _active = new();

            public int PendingCount => _pending.Count;

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
                if (_hash.Add(documentId))
                {
                    _pending.Enqueue(documentId);

                    if (_pending.Count > MaximumPendingCount)
                        MaximumPendingCount = _pending.Count;
                }
            }

            public bool IsEnqueued(DocumentId documentId) =>
                _hash.Contains(documentId);

            public bool IsActive(DocumentId documentId) =>
                _active.ContainsKey(documentId);

            public void Remove(DocumentId documentId)
            {
                if (_hash.Contains(documentId))
                {
                    _hash.Remove(documentId);

                    var backgroundQueueItems = _pending.ToList();

                    _pending.Clear();

                    foreach (var item in backgroundQueueItems)
                    {
                        if (item != documentId)
                            _pending.Enqueue(item);
                    }
                }
            }
            
            public bool TryDequeue([NotNullWhen(true)] out DocumentId? documentId, [NotNullWhen(true)] out CancellationTokenSource? cancellationTokenSource)
            {
                if (_pending.Count > 0)
                {
                    documentId = _pending.Dequeue();

                    _hash.Remove(documentId);

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
                }
            }
        }
    }
}
