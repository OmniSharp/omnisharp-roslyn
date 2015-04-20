#if ASPNET50
using System;
using System.Collections.Generic;
using Microsoft.Framework.Logging;
using OmniSharp.Services;
using OmniSharp.Models;

namespace OmniSharp.MSBuild
{
    public class MSBuildEventLogger : Microsoft.Build.Framework.ILogger
    {
        private readonly IEventEmitter _emitter;
        private readonly ILogger _logger;
        private readonly IList<Action> _callOnShutdown;

        public MSBuildEventLogger(ILogger logger, IEventEmitter emitter)
        {
            _logger = logger;
            _emitter = emitter;
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
            var message = new ProjectStatusMessage()
            {
                LogLevel = "Error",
                FileName = args.ProjectFile,
                Diagnostics = new DiagnosticLocation[]{
                    new DiagnosticLocation() {
                        Line = args.LineNumber,
                        Column = args.ColumnNumber,
                        EndLine = args.EndLineNumber,
                        EndColumn = args.EndColumnNumber,
                        FileName = args.File,
                        Text = args.Message
                    }
                }
            };
            _emitter.Emit(EventTypes.ProjectStatus, message);
            _logger.WriteError(args.Message);
        }

        private void OnWarning(object sender, Microsoft.Build.Framework.BuildWarningEventArgs args)
        {
            var message = new ProjectStatusMessage()
            {
                LogLevel = "Warning",
                FileName = args.ProjectFile,
                Diagnostics = new DiagnosticLocation[]{
                    new DiagnosticLocation() {
                        Line = args.LineNumber,
                        Column = args.ColumnNumber,
                        EndLine = args.EndLineNumber,
                        EndColumn = args.EndColumnNumber,
                        FileName = args.File,
                        Text = args.Message
                    }
                }
            };
            _emitter.Emit(EventTypes.ProjectStatus, message);
            _logger.WriteWarning(args.Message);
        }
    }
}
#endif