using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class AnalyzerWorkerQueueFacts
    {
        [Theory]
        [InlineData(AnalyzerWorkType.Background)]
        [InlineData(AnalyzerWorkType.Foreground)]
        public void WhenItemsAreAddedButThrotlingIsntOverNoWorkShouldBeReturned(AnalyzerWorkType workType)
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(utcNow: () => now);
            var document = CreateTestDocumentId();

            queue.PutWork(new [] { document }, workType);
            Assert.Empty(queue.TakeWork(workType));
        }

        [Theory]
        [InlineData(AnalyzerWorkType.Background)]
        [InlineData(AnalyzerWorkType.Foreground)]
        public void WhenWorksIsAddedToQueueThenTheyWillBeReturned(AnalyzerWorkType workType)
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(utcNow: () => now);
            var document = CreateTestDocumentId();

            queue.PutWork(new [] { document }, workType);

            now = PassOverThrotlingPeriod(now);
            var work = queue.TakeWork(workType);

            Assert.Contains(document, work);
            Assert.Empty(queue.TakeWork(workType));
        }

        [Theory]
        [InlineData(AnalyzerWorkType.Background)]
        [InlineData(AnalyzerWorkType.Foreground)]
        public void WhenSameItemIsAddedMultipleTimesInRowThenThrottleItemAsOne(AnalyzerWorkType workType)
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(utcNow: () => now);
            var document = CreateTestDocumentId();

            queue.PutWork(new [] { document }, workType);
            queue.PutWork(new [] { document }, workType);
            queue.PutWork(new [] { document }, workType);

            Assert.Empty(queue.TakeWork(workType));

            now = PassOverThrotlingPeriod(now);

            Assert.Contains(document, queue.TakeWork(workType));
            Assert.Empty(queue.TakeWork(workType));
        }

        private static DateTime PassOverThrotlingPeriod(DateTime now) => now.AddSeconds(30);

        [Fact]
        public void WhenForegroundWorkIsAddedThenWaitNextIterationOfItReady()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(utcNow: () => now, timeoutForPendingWorkMs: 500);
            var document = CreateTestDocumentId();

            queue.PutWork(new [] { document }, AnalyzerWorkType.Foreground);

            var pendingTask = queue.WaitForegroundWorkComplete();
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));

            Assert.False(pendingTask.IsCompleted);

            now = PassOverThrotlingPeriod(now);

            var work = queue.TakeWork(AnalyzerWorkType.Foreground);
            queue.ForegroundWorkComplete();
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));
            Assert.True(pendingTask.IsCompleted);
        }

        [Fact]
        public void WhenForegroundWorkIsUnderAnalysisOutFromQueueThenWaitUntilNextIterationOfItIsReady()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(utcNow: () => now, timeoutForPendingWorkMs: 500);
            var document = CreateTestDocumentId();

            queue.PutWork(new [] { document }, AnalyzerWorkType.Foreground);

            now = PassOverThrotlingPeriod(now);

            var work = queue.TakeWork(AnalyzerWorkType.Foreground);

            var pendingTask = queue.WaitForegroundWorkComplete();
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));

            Assert.False(pendingTask.IsCompleted);
            queue.ForegroundWorkComplete();
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));
            Assert.True(pendingTask.IsCompleted);
        }

        [Fact]
        public void WhenWorkIsWaitedButTimeoutForWaitIsExceededAllowContinue()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(utcNow: () => now, timeoutForPendingWorkMs: 20);
            var document = CreateTestDocumentId();

            queue.PutWork(new [] { document }, AnalyzerWorkType.Foreground);

            now = PassOverThrotlingPeriod(now);
            var work = queue.TakeWork(AnalyzerWorkType.Foreground);

            var pendingTask = queue.WaitForegroundWorkComplete();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            pendingTask.Wait(cts.Token);

            Assert.True(pendingTask.IsCompleted);
        }

        [Fact]
        public async Task WhenMultipleThreadsAreConsumingAnalyzerWorkerQueueItWorksAsExpected()
        {
            var now = DateTime.UtcNow;

            var queue = new AnalyzerWorkQueue(utcNow: () => now, timeoutForPendingWorkMs: 1000);

            var parallelQueues =
                Enumerable.Range(0, 10)
                    .Select(_ =>
                        Task.Run(() => {
                            var document = CreateTestDocumentId();

                            queue.PutWork(new [] { document }, AnalyzerWorkType.Foreground);

                            now = PassOverThrotlingPeriod(now);

                            var work = queue.TakeWork(AnalyzerWorkType.Foreground);

                            var pendingTask = queue.WaitForegroundWorkComplete();

                            queue.WaitForegroundWorkComplete();

                            pendingTask.Wait(TimeSpan.FromMilliseconds(300));
                    }))
                    .ToArray();

            await Task.WhenAll(parallelQueues);

            Assert.Empty(queue.TakeWork(AnalyzerWorkType.Foreground));
        }

        [Fact]
        public async Task WhenWorkIsAddedAgainWhenPreviousIsAnalysing_ThenDontWaitAnotherOneToGetReady()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(utcNow: () => now);
            var document = CreateTestDocumentId();

            queue.PutWork(new [] { document }, AnalyzerWorkType.Foreground);

            now = PassOverThrotlingPeriod(now);

            var work = queue.TakeWork(AnalyzerWorkType.Foreground);
            var waitingCall = Task.Run(async () => await queue.WaitForegroundWorkComplete());
            await Task.Delay(50);

            // User updates code -> document is queued again during period when theres already api call waiting
            // to continue.
            queue.PutWork(new [] { document }, AnalyzerWorkType.Foreground);

            // First iteration of work is done.
            queue.ForegroundWorkComplete();

            // Waiting call continues because it's iteration of work is done, even when theres next
            // already waiting.
            await waitingCall;

            Assert.True(waitingCall.IsCompleted);
        }

        [Fact]
        public async Task WhenBackgroundWorkIsAdded_DontWaitIt()
        {
            throw new NotImplementedException();
        }

        private DocumentId CreateTestDocumentId()
        {
            var projectInfo = ProjectInfo.Create(
                id: ProjectId.CreateNewId(),
                version: VersionStamp.Create(),
                name: "testProject",
                assemblyName: "AssemblyName",
                language: LanguageNames.CSharp);

            return DocumentId.CreateNewId(projectInfo.Id);
        }
    }
}
