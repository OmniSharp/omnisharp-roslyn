using OmniSharp.Mef;

﻿namespace OmniSharp.Models.v1
{
    [OmniSharpEndpoint(OmniSharpEndpoints.ProjectInformation, typeof(ProjectInformationRequest), typeof(ProjectInformationResponse))]
    public class ProjectInformationRequest : Request { }
}
