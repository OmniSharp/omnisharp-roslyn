using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace OmniSharp.MSBuild.Logging
{
    internal class MSBuildLogger : Microsoft.Build.Utilities.Logger
    {
        private readonly ILogger _logger;
        private readonly List<MSBuildDiagnostic> _diagnostics;

        public MSBuildLogger(ILogger logger)
        {
            _logger = logger;
            _diagnostics = new List<MSBuildDiagnostic>();
        }

        public override void Initialize(Microsoft.Build.Framework.IEventSource eventSource)
        {
            eventSource.ErrorRaised += OnError;
            eventSource.WarningRaised += OnWarning;
        }

        public ImmutableArray<MSBuildDiagnostic> GetDiagnostics() =>
            _diagnostics.ToImmutableArray();

        private void OnError(object sender, Microsoft.Build.Framework.BuildErrorEventArgs args)
        {
            var msBuildDiagnostic = MSBuildDiagnostic.CreateFrom(args);
            _logger.LogError(msBuildDiagnostic.Message);
            _diagnostics.Add(msBuildDiagnostic);
        }

        private void OnWarning(object sender, Microsoft.Build.Framework.BuildWarningEventArgs args)
        {
            _logger.LogWarning(args.Message);
            _diagnostics.Add(MSBuildDiagnostic.CreateFrom(args));
        }
    }
}
