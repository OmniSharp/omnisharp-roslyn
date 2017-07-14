using OmniSharp.Mef;
using OmniSharp.Models;
using System.Composition;
using OmniSharp.Models.CodeCheck;

namespace OmniSharp.Cake.Services.Diagnostics
{
    [OmniSharpHandler(OmniSharpEndpoints.CodeCheck, Constants.LanguageNames.Cake), Shared]
    public class CodeCheckHandler : CakeRequestHandler<CodeCheckRequest, QuickFixResponse>
    {
        [ImportingConstructor]
        public CodeCheckHandler(
            OmniSharpWorkspace workspace)
            : base(workspace)
        {
        }
    }
}
