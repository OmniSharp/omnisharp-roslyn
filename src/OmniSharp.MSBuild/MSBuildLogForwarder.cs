using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OmniSharp.MSBuild.Models.Events;

namespace OmniSharp.MSBuild
{
    public class MSBuildLogForwarder : Microsoft.Build.Framework.ILogger
    {
        private readonly ILogger _logger;
        private readonly ICollection<MSBuildDiagnosticsMessage> _diagnostics;
        private readonly IList<Action> _callOnShutdown;

        public MSBuildLogForwarder(ILogger logger, ICollection<MSBuildDiagnosticsMessage> diagnostics)
        {
            _logger = logger;
            _diagnostics = diagnostics;
            _callOnShutdown = new List<Action>();
        }

        public string Parameters { get; set; }

        public Microsoft.Build.Framework.LoggerVerbosity Verbosity { get; set; }

        public void Initialize(Microsoft.Build.Framework.IEventSource eventSource)
        {
            eventSource.ErrorRaised += OnError;
            eventSource.WarningRaised += OnWarning;
            _callOnShutdown.Add(() =>
            {
                eventSource.ErrorRaised -= OnError;
                eventSource.WarningRaised -= OnWarning;
            });
        }

        public void Shutdown()
        {
            foreach (var action in _callOnShutdown)
            {
                action();
            }
        }

        private void AddDiagnostic(string logLevel, string fileName, string message, int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber)
        {
            if (_diagnostics == null)
            {
                return;
            }

            _diagnostics.Add(new MSBuildDiagnosticsMessage()
            {
                LogLevel = logLevel,
                FileName = fileName,
                Text = message,
                StartLine = lineNumber,
                StartColumn = columnNumber,
                EndLine = endLineNumber,
                EndColumn = endColumnNumber
            });
        }

        private void OnError(object sender, Microsoft.Build.Framework.BuildErrorEventArgs args)
        {
            _logger.LogError(args.Message);

            AddDiagnostic("Error", args.File, args.Message, args.LineNumber, args.ColumnNumber, args.EndLineNumber, args.EndColumnNumber);
        }

        private void OnWarning(object sender, Microsoft.Build.Framework.BuildWarningEventArgs args)
        {
            _logger.LogWarning(args.Message);

            AddDiagnostic("Warning", args.File, args.Message, args.LineNumber, args.ColumnNumber, args.EndLineNumber, args.EndColumnNumber);
        }
    }
}