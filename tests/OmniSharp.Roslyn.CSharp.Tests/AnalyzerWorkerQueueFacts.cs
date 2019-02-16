using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class AnalyzerWorkerQueueFacts
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

        [Fact]
        public void WhenItemsAreAddedButThrotlingIsntOverNoWorkShouldBeReturned()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), utcNow: () => now);
            var document = CreateTestDocument();

            queue.PutWork(document);
            Assert.Empty(queue.TakeWork());
        }

        [Fact]
        public void WhenWorksIsAddedToQueueThenTheyWillBeReturned()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), utcNow: () => now);
            var document = CreateTestDocument();

            queue.PutWork(document);

            now = PassOverThrotlingPeriod(now);
            var work = queue.TakeWork();

            Assert.Contains(document, work);
            Assert.Empty(queue.TakeWork());
        }

        [Fact]
        public void WhenSameItemIsAddedMultipleTimesInRowThenThrottleItemAsOne()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), utcNow: () => now);
            var document = CreateTestDocument();

            queue.PutWork(document);
            queue.PutWork(document);
            queue.PutWork(document);

            Assert.Empty(queue.TakeWork());

            now = PassOverThrotlingPeriod(now);

            Assert.Contains(document, queue.TakeWork());
            Assert.Empty(queue.TakeWork());
        }

        private static DateTime PassOverThrotlingPeriod(DateTime now) => now.AddSeconds(30);

        [Fact]
        public void WhenWorkIsAddedThenWaitNextIterationOfItReady()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), utcNow: () => now, timeoutForPendingWorkMs: 500);
            var document = CreateTestDocument();

            queue.PutWork(document);

            var pendingTask = queue.WaitWorkReadyForDocuments(new [] { document }.ToImmutableArray());
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));

            Assert.False(pendingTask.IsCompleted);

            now = PassOverThrotlingPeriod(now);

            var work = queue.TakeWork();
            queue.MarkWorkAsCompleteForDocument(document);
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));
            Assert.True(pendingTask.IsCompleted);
        }

        [Fact]
        public void WhenWorkIsUnderAnalysisOutFromQueueThenWaitUntilNextIterationOfItIsReady()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), utcNow: () => now, timeoutForPendingWorkMs: 500);
            var document = CreateTestDocument();

            queue.PutWork(document);

            now = PassOverThrotlingPeriod(now);

            var work = queue.TakeWork();

            var pendingTask = queue.WaitWorkReadyForDocuments(work);
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));

            Assert.False(pendingTask.IsCompleted);
            queue.MarkWorkAsCompleteForDocument(document);
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));
            Assert.True(pendingTask.IsCompleted);
        }

        [Fact]
        public void WhenWorkIsWaitedButTimeoutForWaitIsExceededAllowContinue()
        {
            var now = DateTime.UtcNow;
            var loggerFactory = new LoggerFactory();
            var queue = new AnalyzerWorkQueue(loggerFactory, utcNow: () => now, timeoutForPendingWorkMs: 20);
            var document = CreateTestDocument();

            queue.PutWork(document);

            now = PassOverThrotlingPeriod(now);
            var work = queue.TakeWork();

            var pendingTask = queue.WaitWorkReadyForDocuments(work);
            pendingTask.Wait(TimeSpan.FromMilliseconds(100));

            Assert.True(pendingTask.IsCompleted);
            Assert.Contains("Timeout before work got ready", loggerFactory.Logger.RecordedMessages.Single());
        }

        [Fact]
        public async Task WhenMultipleThreadsAreConsumingAnalyzerWorkerQueueItWorksAsExpected()
        {
            var now = DateTime.UtcNow;

            var queue = new AnalyzerWorkQueue(new LoggerFactory(), utcNow: () => now, timeoutForPendingWorkMs: 1000);

            var parallelQueues =
                Enumerable.Range(0, 10)
                    .Select(_ =>
                        Task.Run(() => {
                            var document = CreateTestDocument();

                            queue.PutWork(document);

                            now = PassOverThrotlingPeriod(now);
                            var work = queue.TakeWork();

                            var pendingTask = queue.WaitWorkReadyForDocuments(work);

                            foreach (var workDoc in work)
                            {
                                queue.MarkWorkAsCompleteForDocument(workDoc);
                            }

                            pendingTask.Wait(TimeSpan.FromMilliseconds(300));
                    }))
                    .ToArray();

            await Task.WhenAll(parallelQueues);

            Assert.Empty(queue.TakeWork());
        }

        [Fact]
        public async Task WhenWorkIsAddedAgainWhenPreviousIsAnalysing_ThenDontWaitAnotherOneToGetReady()
        {
            var now = DateTime.UtcNow;
            var loggerFactory = new LoggerFactory();
            var queue = new AnalyzerWorkQueue(loggerFactory, utcNow: () => now);
            var document = CreateTestDocument();

            queue.PutWork(document);

            now = PassOverThrotlingPeriod(now);

            var work = queue.TakeWork();
            var waitingCall = Task.Run(async () => await queue.WaitWorkReadyForDocuments(work));
            await Task.Delay(50);

            // User updates code -> document is queued again during period when theres already api call waiting
            // to continue.
            queue.PutWork(document);

            // First iteration of work is done.
            queue.MarkWorkAsCompleteForDocument(document);

            // Waiting call continues because it's iteration of work is done, even when theres next
            // already waiting.
            await waitingCall;

            Assert.True(waitingCall.IsCompleted);
            Assert.Empty(loggerFactory.Logger.RecordedMessages);
        }

        private Document CreateTestDocument()
        {
            var projectInfo = ProjectInfo.Create(
                    id: ProjectId.CreateNewId(),
                    version: VersionStamp.Create(),
                    name: "testProject",
                    assemblyName: "AssemblyName",
                    language: LanguageNames.CSharp);

            var tempWorkspace = new AdhocWorkspace();
            tempWorkspace.AddProject(projectInfo);
            return tempWorkspace.AddDocument(DocumentInfo.Create(
                id: DocumentId.CreateNewId(projectInfo.Id),
                name: "testFile.cs"));

        }
    }
}
