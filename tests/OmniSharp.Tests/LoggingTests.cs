using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.Services;
using Xunit;

namespace OmniSharp.Tests
{
    using ConfigurationBuilder = Microsoft.Extensions.Configuration.ConfigurationBuilder;

    public class LoggingTests
    {
        private class FakeLoggerProvider : ILoggerProvider
        {
            private readonly IDictionary<LogLevel, List<string>> _logMessages;

            public FakeLoggerProvider(IDictionary<LogLevel, List<string>> logMessages) => _logMessages = logMessages;

            public ILogger CreateLogger(string categoryName) => new FakeLogger(_logMessages);
            public void Dispose() { }
        }

        private class FakeLogger : ILogger
        {
            private readonly IDictionary<LogLevel, List<string>> _logMessages;

            public FakeLogger(IDictionary<LogLevel, List<string>> logMessages) => _logMessages = logMessages;

            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!_logMessages.TryGetValue(logLevel, out var messages))
                {
                    messages = new List<string>();
                    _logMessages.Add(logLevel, messages);
                }

                messages.Add(formatter(state, exception));
            }
        }

        private static (ILogger, IDictionary<LogLevel, List<string>>) CreateLogger(LogLevel logLevel)
        {
            var environment = new OmniSharpEnvironment(logLevel: logLevel);
            var configuration = new ConfigurationBuilder().Build();
            var logMessages = new Dictionary<LogLevel, List<string>>();

            var serviceProvider = CompositionHostBuilder.CreateDefaultServiceProvider(environment, configuration, NullEventEmitter.Instance,
                configureLogging: builder =>
                {
                    builder.AddProvider(new FakeLoggerProvider(logMessages));
                });

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<LoggingTests>();

            return (logger, logMessages);
        }

        [Fact]
        public void TestDebugLogLevel()
        {
            var (logger, logMessages) = CreateLogger(LogLevel.Debug);

            logger.LogDebug("TestDebug");
            logger.LogTrace("TestTrace");

            Assert.Single(logMessages);
            Assert.True(logMessages.ContainsKey(LogLevel.Debug));
            Assert.False(logMessages.ContainsKey(LogLevel.Trace));
        }

        [Fact]
        public void TestInfoLogLevel()
        {
            var (logger, logMessages) = CreateLogger(LogLevel.Information);

            logger.LogInformation("TestInformation");
            logger.LogDebug("TestDebug");
            logger.LogTrace("TestTrace");

            Assert.Single(logMessages);
            Assert.True(logMessages.ContainsKey(LogLevel.Information));
            Assert.False(logMessages.ContainsKey(LogLevel.Debug));
            Assert.False(logMessages.ContainsKey(LogLevel.Trace));
        }

        [Fact]
        public void TestTraceLogLevel()
        {
            var (logger, logMessages) = CreateLogger(LogLevel.Trace);

            logger.LogInformation("TestInformation");
            logger.LogDebug("TestDebug");
            logger.LogTrace("TestTrace");

            Assert.Equal(3, logMessages.Count);
            Assert.True(logMessages.ContainsKey(LogLevel.Information));
            Assert.True(logMessages.ContainsKey(LogLevel.Debug));
            Assert.True(logMessages.ContainsKey(LogLevel.Trace));
        }
    }
}
