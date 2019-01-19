using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
            }

            public ImmutableArray<string> RecordedMessages { get; set; }
        }

        private class LoggerFactory : ILoggerFactory
        {
            public ILogger _logger = new Logger();
            public void AddProvider(ILoggerProvider provider)
            {
            }

            public ILogger CreateLogger(string categoryName)
            {
                return _logger;
            }

            public void Dispose()
            {
            }
        }

        [Fact]
        public void WhenProjectsAreAddedButThrotlingIsntOverNoProjectsShouldBeReturned()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), utcNow: () => now);
            var projectId = ProjectId.CreateNewId();

            queue.PutWork(projectId);
            Assert.Empty(queue.TakeWork());
        }

        [Fact]
        public void WhenProjectsAreAddedToQueueThenTheyWillBeReturned()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), utcNow: () => now);
            var projectId = ProjectId.CreateNewId();

            queue.PutWork(projectId);

            now = PassOverThrotlingPeriod(now);

            Assert.Contains(projectId, queue.TakeWork());
            Assert.Empty(queue.TakeWork());
        }

        [Fact]
        public void WhenSameProjectIsAddedMultipleTimesInRowThenThrottleProjectsAsOne()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), utcNow: () => now);
            var projectId = ProjectId.CreateNewId();

            queue.PutWork(projectId);
            queue.PutWork(projectId);
            queue.PutWork(projectId);

            Assert.Empty(queue.TakeWork());

            now = PassOverThrotlingPeriod(now);

            Assert.Contains(projectId, queue.TakeWork());
            Assert.Empty(queue.TakeWork());
        }

        private static DateTime PassOverThrotlingPeriod(DateTime now) => now.AddSeconds(1);

        [Fact]
        public void WhenWorkIsAddedThenWaitNextIterationOfItReady()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), utcNow: () => now, timeoutForPendingWorkMs: 500);
            var projectId = ProjectId.CreateNewId();

            queue.PutWork(projectId);

            var pendingTask = queue.WaitForPendingWorkDoneEvent(new [] { projectId }.ToImmutableArray());
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));

            Assert.False(pendingTask.IsCompleted);

            now = PassOverThrotlingPeriod(now);

            var work = queue.TakeWork();
            queue.MarkWorkAsCompleteForProject(projectId);
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));
            Assert.True(pendingTask.IsCompleted);
        }

        [Fact]
        public void WhenWorkIsUnderAnalysisOutFromQueueThenWaitUntilNextIterationOfItIsReady()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), utcNow: () => now, timeoutForPendingWorkMs: 500);
            var projectId = ProjectId.CreateNewId();

            queue.PutWork(projectId);

            now = PassOverThrotlingPeriod(now);

            var work = queue.TakeWork();

            var pendingTask = queue.WaitForPendingWorkDoneEvent(work);
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));

            Assert.False(pendingTask.IsCompleted);
            queue.MarkWorkAsCompleteForProject(projectId);
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));
            Assert.True(pendingTask.IsCompleted);
        }

        [Fact]
        public void WhenWorkIsWaitedButTimeoutForWaitIsExceededAllowContinue()
        {
            var now = DateTime.UtcNow;
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), utcNow: () => now, timeoutForPendingWorkMs: 50);
            var projectId = ProjectId.CreateNewId();

            queue.PutWork(projectId);

            now = PassOverThrotlingPeriod(now);
            var work = queue.TakeWork();

            var pendingTask = queue.WaitForPendingWorkDoneEvent(work);
            pendingTask.Wait(TimeSpan.FromMilliseconds(100));

            Assert.True(pendingTask.IsCompleted);
        }

        [Fact]
        public async Task WhenMultipleThreadsAreConsumingAnalyzerWorkerQueueItWorksAsExpected()
        {
            var now = DateTime.UtcNow;

            var queue = new AnalyzerWorkQueue(new LoggerFactory(), utcNow: () => now, timeoutForPendingWorkMs: 50);

            var parallelQueues =
                Enumerable.Range(0, 10)
                    .Select(_ =>
                        Task.Run(() => {
                            var projectId = ProjectId.CreateNewId();

                            queue.PutWork(projectId);

                            PassOverThrotlingPeriod(now);
                            var work = queue.TakeWork();

                            var pendingTask = queue.WaitForPendingWorkDoneEvent(work);
                            pendingTask.Wait(TimeSpan.FromMilliseconds(5));

                            Assert.True(pendingTask.IsCompleted);
                    }))
                    .ToArray();

            await Task.WhenAll(parallelQueues);
        }
    }
}
