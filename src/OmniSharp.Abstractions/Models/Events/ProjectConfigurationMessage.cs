using System.Collections;
using System.Collections.Generic;

namespace OmniSharp.Models.Events
{
    public class ProjectConfigurationMessage
    {
        public string ProjectId { get; set; }
        public string SessionId { get; set; }
        public int OutputKind { get; set; }
        public IEnumerable<string> ProjectCapabilities { get; set; }
        public IEnumerable<string> TargetFrameworks { get; set; }
        public string SdkVersion { get; set; }
        public IEnumerable<string> References { get; set; }
        public IEnumerable<string> FileExtensions { get; set; }
        public IEnumerable<int> FileCounts { get; set; }
    }
}
