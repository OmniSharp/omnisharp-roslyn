using OmniSharp.Mef;

namespace OmniSharp.Models.v1
{
    [OmniSharpEndpoint(OmnisharpEndpoints.ProjectInformation, typeof(ProjectInformationRequest), typeof(ProjectInformationResponse))]
    public class ProjectInformationRequest : Request { }
}
