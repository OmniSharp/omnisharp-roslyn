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
#pragma warning disable VSTHRD103 // Call async methods when in an async method
#pragma warning disable xUnit1031 // Test methods should not use blocking task operations
    public class AsyncAnalyzerWorkerQueueFacts
    {
        private class Logger : ILogger
        {
            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                RecordedMessages = RecordedMessages.Add(state.ToString());
            }

            public ImmutableArray<string> RecordedMessages { get; set; } = ImmutableArray.Create<string>();
        }

        private class LoggerFactory : ILoggerFactory
        {
            public Logger Logger { get; } = new Logger();

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public ILogger CreateLogger(string categoryName)
            {
                return Logger;
            }

            public void Dispose()
            {
            }
        }

        [Theory]
        [InlineData(AnalyzerWorkType.Background)]
        [InlineData(AnalyzerWorkType.Foreground)]
        public async Task WhenWorksIsAddedToQueueThenTheyWillBeReturned(AnalyzerWorkType workType)
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document = CreateTestDocumentId();

            queue.PutWork(new[] { document }, workType);

            var work = await queue.TakeWorkAsyncWithTimeout();

            Assert.Equal(document, work.DocumentId);
            Assert.Equal(0, queue.PendingCount);
        }

        [Fact]
        public async Task WhenForegroundWorkIsAddedThenWaitNextIterationOfItReady()
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document = CreateTestDocumentId();

            queue.PutWork(new[] { document }, AnalyzerWorkType.Foreground);

            var pendingTask = queue.WaitForegroundWorkCompleteWithTimeout(500);

            Assert.False(pendingTask.IsCompleted);

            var work = await queue.TakeWorkAsyncWithTimeout();

            queue.WorkComplete(work);

            pendingTask.Wait(TimeSpan.FromMilliseconds(50));

            Assert.True(pendingTask.IsCompleted);
        }

        [Fact]
        public async Task WhenForegroundWorkIsUnderAnalysisOutFromQueueThenWaitUntilNextIterationOfItIsReady()
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document = CreateTestDocumentId();

            queue.PutWork(new[] { document }, AnalyzerWorkType.Foreground);

            var work = await queue.TakeWorkAsync();

            var pendingTask = queue.WaitForegroundWorkCompleteWithTimeout(500);
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));

            Assert.False(pendingTask.IsCompleted);
            queue.WorkComplete(work);
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));
            Assert.True(pendingTask.IsCompleted);
        }

        [Fact]
        public async Task WhenMultipleThreadsAreConsumingAnalyzerWorkerQueueItWorksAsExpected()
        {
            var now = DateTime.UtcNow;

            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());

            var parallelQueues =
                Enumerable.Range(0, 10)
                    .Select(_ =>
                        Task.Run(async () =>
                        {
                            var document = CreateTestDocumentId();

                            queue.PutWork(new[] { document }, AnalyzerWorkType.Foreground);

                            var work = await queue.TakeWorkAsync();

                            var pendingTask = queue.WaitForegroundWorkCompleteWithTimeout(1000);

                            var pendingTask2 = queue.WaitForegroundWorkCompleteWithTimeout(1000);

                            pendingTask.Wait(TimeSpan.FromMilliseconds(300));
                        }))
                    .ToArray();

            await Task.WhenAll(parallelQueues);

            Assert.Equal(0, queue.PendingCount);
        }

        [Fact]
        public async Task WhenNewWorkIsAddedAgainWhenPreviousIsAnalysing_ThenDontWaitAnotherOneToGetReady()
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document1 = CreateTestDocumentId();
            var document2 = CreateTestDocumentId();

            queue.PutWork(new[] { document1 }, AnalyzerWorkType.Foreground);

            var work = await queue.TakeWorkAsync();
            var waitingCall = Task.Run(async () => await queue.WaitForegroundWorkCompleteWithTimeout(10 * 1000));
            await Task.Delay(50);

            // User updates code -> document is queued again during period when theres already api call waiting
            // to continue.
            queue.PutWork(new[] { document2 }, AnalyzerWorkType.Foreground);

            // First iteration of work is done.
            queue.WorkComplete(work);

            // Waiting call continues because its iteration of work is done, even when theres next
            // already waiting.
            waitingCall.Wait(50);

            Assert.True(waitingCall.IsCompleted);
        }

        [Fact]
        public async Task WhenWorkIsAddedAgainWhenPreviousIsAnalysing_ThenContinueWaiting()
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document = CreateTestDocumentId();

            queue.PutWork(new[] { document }, AnalyzerWorkType.Foreground);

            var work = await queue.TakeWorkAsync();
            var waitingCall = Task.Run(async () => await queue.WaitForegroundWorkCompleteWithTimeout(10 * 1000));
            await Task.Delay(50);

            // User updates code -> document is queued again during period when theres already api call waiting
            // to continue.
            queue.PutWork(new[] { document }, AnalyzerWorkType.Foreground);

            // First iteration of work is done.
            queue.WorkComplete(work);

            // Waiting call continues because its iteration of work is done, even when theres next
            // already waiting.
            waitingCall.Wait(50);

            Assert.False(waitingCall.IsCompleted);
        }

        [Fact]
        public void WhenBackgroundWorkIsAdded_DontWaitIt()
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document = CreateTestDocumentId();

            queue.PutWork(new[] { document }, AnalyzerWorkType.Background);

            Assert.True(queue.WaitForegroundWorkComplete().IsCompleted);
        }

        [Fact]
        public void WhenSingleFileIsPromoted_ThenPromoteItFromBackgroundQueueToForeground()
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document = CreateTestDocumentId();

            queue.PutWork(new[] { document }, AnalyzerWorkType.Background);

            queue.TryPromote(document);

            Assert.NotEqual(0, queue.PendingCount);
        }

        [Fact]
        public void WhenFileIsntAtBackgroundQueueAndTriedToBePromoted_ThenDontDoNothing()
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document = CreateTestDocumentId();

            queue.TryPromote(document);

            Assert.Equal(0, queue.PendingCount);
        }

        [Fact]
        public async Task WhenFileIsProcessingInBackgroundQueue_ThenPromoteItAsForeground()
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document = CreateTestDocumentId();

            queue.PutWork(new[] { document }, AnalyzerWorkType.Background);

            await queue.TakeWorkAsyncWithTimeout();

            queue.TryPromote(document);

            await queue.TakeWorkAsyncWithTimeout();
        }

        [Fact]
        public async Task WhenFileIsAddedMultipleTimes_DuplicatesAreIgnored()
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document = CreateTestDocumentId();

            queue.PutWork(new[] { document }, AnalyzerWorkType.Background);

            queue.PutWork(new[] { document }, AnalyzerWorkType.Background);

            await queue.TakeWorkAsyncWithTimeout();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                queue.TakeWorkAsyncWithTimeout());
        }

        [Fact]
        public async Task WhenFileIsAddedWhileProcessing_ThePeviousRunIsCancelled()
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document = CreateTestDocumentId();

            queue.PutWork(new[] { document }, AnalyzerWorkType.Background);

            var result = await queue.TakeWorkAsyncWithTimeout();

            Assert.False(result.CancellationToken.IsCancellationRequested);

            queue.PutWork(new[] { document }, AnalyzerWorkType.Background);

            Assert.True(result.CancellationToken.IsCancellationRequested);
        }

        [Fact]
        public async Task WhenQueueIsEmpty_TakeWorkRespondsToCancellation()
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document = CreateTestDocumentId();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                queue.TakeWorkAsyncWithTimeout());
        }

        [Fact]
        public async Task WhenAwaitingForForgroundWork_CancellationIsHandled()
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document = CreateTestDocumentId();

            queue.PutWork(new[] { document }, AnalyzerWorkType.Foreground);

            var isCancelled = await queue.WaitForegroundWorkCompleteWithTimeout(50);

            Assert.True(isCancelled);
        }

        [Fact]
        public async Task WhenDequeingWork_ItsReturnedInOrderForgroundFirst()
        {
            var queue = new AsyncAnalyzerWorkQueue(new LoggerFactory());
            var document1 = CreateTestDocumentId();
            var document2 = CreateTestDocumentId();
            var document3 = CreateTestDocumentId();
            var document4 = CreateTestDocumentId();

            queue.PutWork(new[] { document3 }, AnalyzerWorkType.Background);

            queue.PutWork(new[] { document1 }, AnalyzerWorkType.Foreground);

            queue.PutWork(new[] { document4 }, AnalyzerWorkType.Background);

            queue.PutWork(new[] { document2 }, AnalyzerWorkType.Foreground);

            var result1 = await queue.TakeWorkAsyncWithTimeout();

            Assert.Equal(document1, result1.DocumentId);

            var result2 = await queue.TakeWorkAsyncWithTimeout();

            Assert.Equal(document2, result2.DocumentId);

            var result3 = await queue.TakeWorkAsyncWithTimeout();

            Assert.Equal(document3, result3.DocumentId);

            var result4 = await queue.TakeWorkAsyncWithTimeout();

            Assert.Equal(document4, result4.DocumentId);
        }

        private static DocumentId CreateTestDocumentId()
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

    public static class AsyncAnalyzerWorkerQueueFactsExtensions
    {
        public static async Task<AsyncAnalyzerWorkQueue.QueueItem> TakeWorkAsyncWithTimeout(this AsyncAnalyzerWorkQueue queue)
        {
            using var cts = new CancellationTokenSource(50);

            return await queue.TakeWorkAsync(cts.Token);
        }

        public static async Task<bool> WaitForegroundWorkCompleteWithTimeout(this AsyncAnalyzerWorkQueue queue, int timeout)
        {
            using var cts = new CancellationTokenSource(timeout);

            await queue.WaitForegroundWorkComplete(cts.Token);

            return cts.Token.IsCancellationRequested;
        }
    }
#pragma warning restore VSTHRD103 // Call async methods when in an async method
}
