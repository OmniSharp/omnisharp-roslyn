using OmniSharp.ScriptCs;

namespace OmniSharp.Models
{
    public class WorkspaceInformationResponse
    {
        public AspNet5WorkspaceInformation AspNet5 { get; set; }
        public MsBuildWorkspaceInformation MSBuild { get; set; }
        public ScriptCsContext ScriptCs { get; set; }
    }
}
