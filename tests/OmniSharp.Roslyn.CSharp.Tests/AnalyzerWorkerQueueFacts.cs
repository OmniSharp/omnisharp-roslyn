using System;
using System.Collections.Immutable;
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
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), throttleWorkMs: 500);
            var projectId = ProjectId.CreateNewId();

            queue.PushWork(projectId);
            Assert.Empty(queue.PopWork());
        }

        [Fact]
        public void WhenProjectsAreAddedToQueueThenTheyWillBeReturned()
        {
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), throttleWorkMs: 0);
            var projectId = ProjectId.CreateNewId();

            queue.PushWork(projectId);

            Assert.Contains(projectId, queue.PopWork());
            Assert.Empty(queue.PopWork());
        }

        [Fact]
        public async Task WhenSameProjectIsAddedMultipleTimesInRowThenThrottleProjectsAsOne()
        {
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), throttleWorkMs: 20);
            var projectId = ProjectId.CreateNewId();

            queue.PushWork(projectId);
            queue.PushWork(projectId);
            queue.PushWork(projectId);

            Assert.Empty(queue.PopWork());

            await Task.Delay(TimeSpan.FromMilliseconds(40));

            Assert.Contains(projectId, queue.PopWork());
            Assert.Empty(queue.PopWork());
        }

        [Fact]
        public void WhenWorkIsAddedThenWaitNextIterationOfItReady()
        {
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), throttleWorkMs: 0, timeoutForPendingWorkMs: 500);
            var projectId = ProjectId.CreateNewId();

            queue.PushWork(projectId);

            var pendingTask = queue.WaitForPendingWork(new [] { projectId }.ToImmutableArray());
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));

            Assert.False(pendingTask.IsCompleted);

            var work = queue.PopWork();
            queue.AckWork(projectId);
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));
            Assert.True(pendingTask.IsCompleted);
        }

        [Fact]
        public void WhenWorkIsUnderAnalysisOutFromQueueThenWaitUntilNextIterationOfItIsReady()
        {
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), throttleWorkMs: 0, timeoutForPendingWorkMs: 500);
            var projectId = ProjectId.CreateNewId();

            queue.PushWork(projectId);

            var work = queue.PopWork();

            var pendingTask = queue.WaitForPendingWork(work);
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));

            Assert.False(pendingTask.IsCompleted);

            queue.AckWork(projectId);
            pendingTask.Wait(TimeSpan.FromMilliseconds(50));
            Assert.True(pendingTask.IsCompleted);
        }

        [Fact]
        public void WhenWorkIsWaitedButTimeoutForWaitIsExceededAllowContinue()
        {
            var queue = new AnalyzerWorkQueue(new LoggerFactory(), throttleWorkMs: 0, timeoutForPendingWorkMs: 50);
            var projectId = ProjectId.CreateNewId();

            queue.PushWork(projectId);

            var work = queue.PopWork();

            var pendingTask = queue.WaitForPendingWork(work);
            pendingTask.Wait(TimeSpan.FromMilliseconds(100));

            Assert.True(pendingTask.IsCompleted);
        }
    }
}
