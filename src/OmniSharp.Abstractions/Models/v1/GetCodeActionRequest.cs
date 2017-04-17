using OmniSharp.Mef;

﻿namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.GetCodeAction, typeof(GetCodeActionRequest), typeof(GetCodeActionsResponse))]
    public class GetCodeActionRequest : CodeActionRequest { }
}
