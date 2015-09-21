using OmniSharp.Mef;

﻿namespace OmniSharp.Models.v1
{
    [OmniSharpEndpoint("/project", typeof(ProjectInformationRequest), typeof(ProjectInformationResponse))]
    public class ProjectInformationRequest : Request { }
}
