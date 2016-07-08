using System;
using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.Diagnostics, typeof(DiagnosticsRequest), typeof(DiagnosticsResponse))]
    public class DiagnosticsRequest : Request
    {
    }

    public class DiagnosticsResponse : IAggregateResponse
    {
        public IAggregateResponse Merge(IAggregateResponse response) { return response; }
    }
}
