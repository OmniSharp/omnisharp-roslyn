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
        public async Task<IActionResult> CodeCheck(Request request)
        {
            var quickFixes = new List<QuickFix>();

            var documents = _workspace.GetDocuments(request.FileName);

            foreach (var document in documents)
            {
                var semanticModel = await document.GetSemanticModelAsync();

                foreach (var quickFix in semanticModel.GetDiagnostics().Select(MakeQuickFix))
                {
                    var existingQuickFix = quickFixes.FirstOrDefault(q => q.Equals(quickFix));
                    if (existingQuickFix == null)
                    {
                        quickFix.Projects.Add(document.Project.Name);
                        quickFixes.Add(quickFix);
                    }
                    else
                    {
                        existingQuickFix.Projects.Add(document.Project.Name);
                    }
                }
            }

            return new ObjectResult(new { QuickFixes = quickFixes });
        }

        private static QuickFix MakeQuickFix(Diagnostic diagnostic)
        {
            var span = diagnostic.Location.GetMappedLineSpan();
            return new DiagnosticLocation
            {
                FileName = span.Path,
                Line = span.StartLinePosition.Line + 1,
                Column = span.StartLinePosition.Character + 1,
                EndLine = span.EndLinePosition.Line + 1,
                EndColumn = span.EndLinePosition.Character + 1,
                Text = diagnostic.GetMessage(),
                LogLevel = diagnostic.Severity.ToString()
            };
        }
    }
}