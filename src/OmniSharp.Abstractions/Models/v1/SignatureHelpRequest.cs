using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.SignatureHelp, typeof(SignatureHelpRequest), typeof(SignatureHelp))]
    public class SignatureHelpRequest : Request
    {
    }
}
