using OmniSharp.Mef;

﻿namespace OmniSharp.Models.CodeAction
{
    [OmniSharpEndpoint(OmniSharpEndpoints.RunCodeAction, typeof(RunCodeActionRequest), typeof(RunCodeActionResponse))]
    public class RunCodeActionRequest : CodeActionRequest { }
}
