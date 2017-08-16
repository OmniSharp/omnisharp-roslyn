using System.Composition;
using OmniSharp.Mef;
using OmniSharp.Models.SignatureHelp;

namespace OmniSharp.Cake.Services.RequestHandlers.Signatures
{
    [OmniSharpHandler(OmniSharpEndpoints.SignatureHelp, Constants.LanguageNames.Cake), Shared]
    public class SignatureHelpHandler : CakeRequestHandler<SignatureHelpRequest, SignatureHelpResponse>
    {
        [ImportingConstructor]
        public SignatureHelpHandler(OmniSharpWorkspace workspace) : base(workspace)
        {
        }
    }
}
