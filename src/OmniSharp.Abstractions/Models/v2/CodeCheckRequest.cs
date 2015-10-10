using System;
using OmniSharp.Mef;

namespace OmniSharp.Models.V2
{
    [OmniSharpEndpoint(OmnisharpEndpoints.V2.CodeCheck, typeof(CodeCheckRequest), typeof(CodeCheckResponse))]
    public class CodeCheckRequest : Request
    {
    }

    public class CodeCheckResponse : IAggregateResponse
    {
        public IAggregateResponse Merge(IAggregateResponse response) { }
    }
}
