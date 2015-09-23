using OmniSharp.Mef;

namespace OmniSharp.Models.v1
{
    [OmniSharpEndpoint("/projects", typeof(WorkspaceInformationRequest), typeof(WorkspaceInformationResponse))]
    public class WorkspaceInformationRequest : IRequest
    {
        public bool ExcludeSourceFiles { get; set; }
    }
}
