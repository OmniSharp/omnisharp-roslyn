#if ASPNET50
using System;
using System.Collections.Generic;
using Microsoft.Framework.Logging;

namespace OmniSharp.MSBuild
{
    public class MSBuildLogForwarder : Microsoft.Build.Framework.ILogger
    {
        private readonly ILogger _logger;
        private readonly IList<Action> _callOnShutdown;

        public MSBuildLogForwarder(ILogger logger)
        {
            _logger = logger;
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
            _logger.WriteError(args.Message);
        }

        private void OnWarning(object sender, Microsoft.Build.Framework.BuildWarningEventArgs args)
        {
            _logger.WriteWarning(args.Message);
        }
    }
}
#endif