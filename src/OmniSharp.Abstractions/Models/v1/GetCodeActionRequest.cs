using OmniSharp.Mef;

﻿namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/getcodeactions", typeof(GetCodeActionRequest), typeof(GetCodeActionsResponse))]
    public class GetCodeActionRequest : CodeActionRequest { }
}
