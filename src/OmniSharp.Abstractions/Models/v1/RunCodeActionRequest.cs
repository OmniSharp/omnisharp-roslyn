using OmniSharp.Mef;

﻿namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/runcodeaction", typeof(RunCodeActionRequest), typeof(RunCodeActionResponse))]
    public class RunCodeActionRequest : CodeActionRequest { }
}
