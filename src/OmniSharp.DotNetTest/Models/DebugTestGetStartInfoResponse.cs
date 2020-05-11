using System.Collections.Generic;

namespace OmniSharp.DotNetTest.Models
{
    public class DebugTestGetStartInfoResponse
    {
        public string FileName { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public IDictionary<string, string> EnvironmentVariables { get; set; }
        public bool Succeeded { get; set; }
        public bool ContextHadNoTests { get; set; }
        public string FailureReason { get; set; }
    }
}
