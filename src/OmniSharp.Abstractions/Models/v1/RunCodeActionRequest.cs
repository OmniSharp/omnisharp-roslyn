using OmniSharp.Mef;

﻿namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.RunCodeAction, typeof(RunCodeActionRequest), typeof(RunCodeActionResponse))]
    public class RunCodeActionRequest : CodeActionRequest { }
}
