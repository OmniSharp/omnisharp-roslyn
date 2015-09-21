using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/signatureHelp", typeof(SignatureHelpRequest), typeof(SignatureHelp))]
    public class SignatureHelpRequest : Request
    {
    }
}
