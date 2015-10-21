using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.SignatureHelp, typeof(SignatureHelpRequest), typeof(SignatureHelp))]
    public class SignatureHelpRequest : Request
    {
    }
}
