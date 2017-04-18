using OmniSharp.Mef;

﻿namespace OmniSharp.Models.CodeAction
{
    [OmniSharpEndpoint(OmniSharpEndpoints.GetCodeAction, typeof(GetCodeActionRequest), typeof(GetCodeActionsResponse))]
    public class GetCodeActionRequest : CodeActionRequest { }
}
