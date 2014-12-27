using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("codecheck")]
        public async Task<IActionResult> CodeCheck([FromBody]Request request)
        {
            _workspace.EnsureBufferUpdated(request);

            var quickFixes = new List<QuickFix>();

            var document = _workspace.GetDocument(request.FileName);

            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();

                quickFixes.AddRange(semanticModel.GetDiagnostics().Select(MakeQuickFix));
            }

            return new ObjectResult(new { QuickFixes = quickFixes });
        }

        private static QuickFix MakeQuickFix(Diagnostic diagnostic)
        {
            var span = diagnostic.Location.GetMappedLineSpan();
            var quickFix = new QuickFix();
            quickFix.FileName = span.Path;
            quickFix.Line = span.StartLinePosition.Line + 1;
            quickFix.Column = span.StartLinePosition.Character + 1;
            quickFix.EndLine = span.EndLinePosition.Line + 1;
            quickFix.EndColumn = span.EndLinePosition.Character + 1;
            quickFix.Text = diagnostic.GetMessage();
            quickFix.LogLevel = diagnostic.Severity.ToString();

            return quickFix;
        }
    }
}