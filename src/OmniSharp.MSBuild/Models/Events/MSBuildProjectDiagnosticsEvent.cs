using System.Collections.Generic;

namespace OmniSharp.MSBuild.Models.Events
{
    public class MSBuildProjectDiagnosticsEvent
    {
        public const string Id = "MsBuildProjectDiagnostics";

        public string FileName { get; set; }
        public IEnumerable<MSBuildDiagnosticsMessage> Warnings { get; set; }
        public IEnumerable<MSBuildDiagnosticsMessage> Errors { get; set; }
    }
}