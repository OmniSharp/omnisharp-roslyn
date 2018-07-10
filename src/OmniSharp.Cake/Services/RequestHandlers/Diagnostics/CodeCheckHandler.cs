using System;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Cake.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Utilities;

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

        protected override bool IsValid(CodeCheckRequest request) =>
            !string.IsNullOrEmpty(request.FileName);

        protected override Task<QuickFixResponse> TranslateResponse(QuickFixResponse response, CodeCheckRequest request) =>
            Task.FromResult(response.OnlyThisFile(request.FileName));
    }
}
