using System.Collections;
using System.Collections.Generic;

namespace OmniSharp.Models.Events
{
    public class ProjectConfigurationMessage
    {
        public string ProjectId { get; set; }
        public IEnumerable<string> TargetFrameworks { get; set; }
        public IEnumerable<string> References { get; set; }
        public IEnumerable<string> FileExtensions { get; set; }
    }
}
