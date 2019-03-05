using System.Collections.Generic;

namespace OmniSharp.Models.Events
{
    public class ProjectConfigurationMessage
    {
        public string ProjectFilePath { get; set; }
        public IEnumerable<string> TargetFrameworks { get; set; }
        public IEnumerable<string> References { get; set; }
    }
}
