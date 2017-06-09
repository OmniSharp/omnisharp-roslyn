using OmniSharp.Mef;

namespace OmniSharp.Models.WorkspaceInformation
{
    [OmniSharpEndpoint(OmniSharpEndpoints.WorkspaceInformation, typeof(WorkspaceInformationRequest), typeof(WorkspaceInformationResponse))]
    public class WorkspaceInformationRequest : IRequest
    {
        public bool ExcludeSourceFiles { get; set; }
    }
}
