using OmniSharp.Mef;

namespace OmniSharp.Models.Diagnostics
{
    [OmniSharpEndpoint(OmniSharpEndpoints.Diagnostics, typeof(DiagnosticsRequest), typeof(DiagnosticsResponse))]
    public class DiagnosticsRequest : Request
    {
    }
}
