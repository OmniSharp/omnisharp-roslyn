using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [OmniSharpHandler(typeof(RequestHandler<CodeCheckRequest, QuickFixResponse>), LanguageNames.CSharp)]
    public class CodeCheckService : RequestHandler<CodeCheckRequest, QuickFixResponse>
    {
        private OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public CodeCheckService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<QuickFixResponse> Handle(CodeCheckRequest request)
        {
            var quickFixes = new List<QuickFix>();

            var documents = request.FileName != null
                ? _workspace.GetDocuments(request.FileName)
                : _workspace.CurrentSolution.Projects.SelectMany(project => project.Documents);

            foreach (var document in documents)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                IEnumerable<Diagnostic> diagnostics = semanticModel.GetDiagnostics();

                //script files can have custom directives such as #load which will be deemed invalid by Roslyn
                //we suppress the CS1024 diagnostic for script files for this reason. Roslyn will fix it later too, so this is temporary.
                if (document.SourceCodeKind != SourceCodeKind.Regular)
                {
                    diagnostics = diagnostics.Where(diagnostic => diagnostic.Id != "CS1024");
                }

                foreach (var quickFix in diagnostics.Select(MakeQuickFix))
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

            return new QuickFixResponse(quickFixes);
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
