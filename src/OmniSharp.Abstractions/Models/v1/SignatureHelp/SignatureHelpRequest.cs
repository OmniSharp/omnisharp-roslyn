using OmniSharp.Mef;

namespace OmniSharp.Models.SignatureHelp
{
    [OmniSharpEndpoint(OmniSharpEndpoints.SignatureHelp, typeof(SignatureHelpRequest), typeof(SignatureHelpResponse))]
    public class SignatureHelpRequest : Request
    {
    }
}
