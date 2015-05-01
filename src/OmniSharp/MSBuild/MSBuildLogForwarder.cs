#if DNX451
using System;
using System.Collections.Generic;
using Microsoft.Framework.Logging;
using OmniSharp.Models;

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

        private void OnError(object sender, Microsoft.Build.Framework.BuildErrorEventArgs args)
        {
            _logger.LogError(args.Message);
            _diagnostics.Add(new MSBuildDiagnosticsMessage()
            {
                LogLevel = "Error",
                FileName = args.File,
                Text = args.Message,
                StartLine = args.LineNumber,
                StartColumn = args.ColumnNumber,
                EndLine = args.EndLineNumber,
                EndColumn = args.EndColumnNumber
            });
        }

        private void OnWarning(object sender, Microsoft.Build.Framework.BuildWarningEventArgs args)
        {
            _logger.LogWarning(args.Message);
            _diagnostics.Add(new MSBuildDiagnosticsMessage()
            {
                LogLevel = "Warning",
                FileName = args.File,
                Text = args.Message,
                StartLine = args.LineNumber,
                StartColumn = args.ColumnNumber,
                EndLine = args.EndLineNumber,
                EndColumn = args.EndColumnNumber
            });
        }
    }
}
#endif