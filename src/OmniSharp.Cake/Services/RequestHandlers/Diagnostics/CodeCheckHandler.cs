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

        public override Task<QuickFixResponse> HandleCore(CodeCheckRequest request, IRequestHandler<CodeCheckRequest, QuickFixResponse> service)
        {
            if (string.IsNullOrEmpty(request.FileName))
            {
                return Task.FromResult(new QuickFixResponse());
            }

            return service.Handle(request);
        }
    }
}
