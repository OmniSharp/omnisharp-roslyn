using System;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        public override bool IsValid(CodeCheckRequest request) =>
            !string.IsNullOrEmpty(request.FileName);

        protected override Task<QuickFixResponse> TranslateResponse(QuickFixResponse response, CodeCheckRequest request)
        {
            if (response?.QuickFixes == null)
            {
                return Task.FromResult(response);
            }

            var quickFixes = response.QuickFixes.Where(x => PathsAreEqual(x.FileName, request.FileName));
            response.QuickFixes = quickFixes;
            return Task.FromResult(response);

            bool PathsAreEqual(string x, string y)
            {
                if (x == null && y == null)
                {
                    return true;
                }
                if (x == null || y == null)
                {
                    return false;
                }

                var comparer = PlatformHelper.IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                return Path.GetFullPath(x).Equals(Path.GetFullPath(y), comparer);
            }
        }
    }
}
