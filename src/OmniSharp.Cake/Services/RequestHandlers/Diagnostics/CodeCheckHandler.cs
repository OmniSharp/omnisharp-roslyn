using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;

namespace OmniSharp.Cake.Services.RequestHandlers.Diagnostics
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

        public override bool IsValid(CodeCheckRequest request) =>
            !string.IsNullOrEmpty(request.FileName);
    }
}
