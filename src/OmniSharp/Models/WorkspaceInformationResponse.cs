using OmniSharp.ScriptCs;

namespace OmniSharp.Models
{
    public class WorkspaceInformationResponse
    {
        public DnxWorkspaceInformation Dnx { get; set; }
        public MsBuildWorkspaceInformation MSBuild { get; set; }
        public ScriptCsContext ScriptCs { get; set; }
    }
}
