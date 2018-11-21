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
        }

        private class LoggerFactory : ILoggerFactory
        {
            public void AddProvider(ILoggerProvider provider)
            {
            }

            public ILogger CreateLogger(string categoryName)
            {
                return NullLogger.Instance;
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
        public void WhenWorkIsTakenThenItWillBlockWhenWorkIsWaited()
        {
            // var queue = new AnalyzerWorkQueue(new LoggerFactory(), throttleWorkMs: 0, timeoutForPendingWorkMs: 500);
            // var projectId = ProjectId.CreateNewId();

            // queue.PushWork(projectId);

            // var work = queue.PopWork();

            // queue.WaitForPendingWork(work).Wait(TimeSpan.FromMilliseconds(50));
        }
    }
}