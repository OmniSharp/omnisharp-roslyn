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
        private readonly TimeSpan _foregroundThrottling = TimeSpan.FromMilliseconds(300);
        private readonly TimeSpan _backgroundThrottling = TimeSpan.FromMilliseconds(2000);

        private ImmutableHashSet<DocumentId> _backgroundWork = ImmutableHashSet<DocumentId>.Empty;
        private ImmutableHashSet<DocumentId> _foregroundWork = ImmutableHashSet<DocumentId>.Empty;
        private DateTime _foregroundWorkThrottlingBeginStamp = DateTime.UtcNow;
        private DateTime _backgroundThrottlingBeginStamp = DateTime.UtcNow;
        private CancellationTokenSource _foregroundWorkPending = new CancellationTokenSource();

        private readonly Func<DateTime> _utcNow;
        private readonly int _maximumDelayWhenWaitingForResults;

        public AnalyzerWorkQueue(Func<DateTime> utcNow = null, int timeoutForPendingWorkMs = 15*1000)
        {
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _maximumDelayWhenWaitingForResults = timeoutForPendingWorkMs;
        }

        public void PutWork(IReadOnlyCollection<DocumentId> documentIds, AnalyzerWorkType workType)
        {
            if(workType == AnalyzerWorkType.Background)
            {
                if(_backgroundWork.IsEmpty)
                    _backgroundThrottlingBeginStamp = _utcNow();

                _backgroundWork = _backgroundWork.Union(documentIds);
            }
            else
            {
                if(_foregroundWork.IsEmpty)
                    _foregroundWorkThrottlingBeginStamp = _utcNow();

                if(_foregroundWorkPending == null)
                    _foregroundWorkPending = new CancellationTokenSource();

                _foregroundWork = _foregroundWork.Union(documentIds);
            }
        }

        public IReadOnlyCollection<DocumentId> TakeWork(AnalyzerWorkType workType)
        {
            if(workType == AnalyzerWorkType.Foreground)
            {
                return TakeForegroundWork();
            }
            else
            {
                return TakeBackgroundWork();
            }
        }

        private IReadOnlyCollection<DocumentId> TakeForegroundWork()
        {
            if (IsForegroundThrottlingActive() || _foregroundWork.IsEmpty)
                return ImmutableHashSet<DocumentId>.Empty;

            lock (_foregroundWork)
            {
                var currentWork = _foregroundWork;
                _foregroundWork = ImmutableHashSet<DocumentId>.Empty;
                return currentWork;
            }
        }

        private IReadOnlyCollection<DocumentId> TakeBackgroundWork()
        {
            if (IsBackgroundThrottlineActive() || _backgroundWork.IsEmpty)
                return ImmutableHashSet<DocumentId>.Empty;

            lock (_backgroundWork)
            {
                var currentWork = _backgroundWork;
                _backgroundWork = ImmutableHashSet<DocumentId>.Empty;
                return currentWork;
            }
        }

        private bool IsForegroundThrottlingActive()
        {
            return (_utcNow() - _foregroundWorkThrottlingBeginStamp).TotalMilliseconds <= _foregroundThrottling.TotalMilliseconds;
        }

        private bool IsBackgroundThrottlineActive()
        {
            return (_utcNow() - _backgroundThrottlingBeginStamp).TotalMilliseconds <= _backgroundThrottling.TotalMilliseconds;
        }

        public void ForegroundWorkComplete()
        {
            lock(_foregroundWork)
            {
                if(_foregroundWorkPending == null)
                    return;

                _foregroundWorkPending.Cancel();
            }
        }

        // Omnisharp V2 api expects that it can request current information of diagnostics any time,
        // however analysis is worker based and is eventually ready. This method is used to make api look
        // like it's syncronous even that actual analysis may take a while.
        public Task WaitForegroundWorkComplete()
        {
            if(_foregroundWorkPending == null || _foregroundWork.IsEmpty)
                return Task.CompletedTask;

            return Task.Delay(_maximumDelayWhenWaitingForResults, _foregroundWorkPending.Token);
        }
    }
}
