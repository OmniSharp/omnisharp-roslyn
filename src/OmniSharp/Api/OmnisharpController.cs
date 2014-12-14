using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    [Route("/")]
    public class OmnisharpController
    {
        private readonly OmnisharpWorkspace _workspace;

        public OmnisharpController(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        [HttpPost("gotodefinition")]
        public async Task<IActionResult> GotoDefinition([FromBody]Request request)
        {
            _workspace.EnsureBufferUpdated(request);

            var quickFixes = new List<QuickFix>();

            var documentIds = _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName);
            var documentId = documentIds.FirstOrDefault();
            var response = new GotoDefinitionResponse();
            if (documentId != null)
            {
                var document = _workspace.CurrentSolution.GetDocument(documentId);
                var semanticModel = await document.GetSemanticModelAsync();
                var syntaxTree = semanticModel.SyntaxTree;
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column ));
                var symbol = SymbolFinder.FindSymbolAtPosition(semanticModel, position, _workspace);
                var lineSpan = symbol.Locations.First().GetMappedLineSpan();
                response = new GotoDefinitionResponse
                {
                    FileName = lineSpan.Path,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1
                };
            }

            return new ObjectResult(response);
        }

        [HttpPost("codecheck")]
        public async Task<IActionResult> CodeCheck([FromBody]Request request)
        {
            _workspace.EnsureBufferUpdated(request);

            var quickFixes = new List<QuickFix>();

            var documentIds = _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName);

            var documentId = documentIds.FirstOrDefault();

            if (documentId != null)
            {
                var document = _workspace.CurrentSolution.GetDocument(documentId);
                var semanticModel = await document.GetSemanticModelAsync();

                quickFixes.AddRange(semanticModel.GetDiagnostics().Select(MakeQuickFix));
            }

            return new ObjectResult(new { QuickFixes = quickFixes });
        }

        [HttpPost("autocomplete")]
        public async Task<IActionResult> AutoComplete([FromBody]Request request)
        {
            var completions = Enumerable.Empty<AutoCompleteResponse>();

            _workspace.EnsureBufferUpdated(request);

            var documentIds = _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName);

            var documentId = documentIds.FirstOrDefault();
            if (documentId != null)
            {
                var document = _workspace.CurrentSolution.GetDocument(documentId);
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column));
                var model = await document.GetSemanticModelAsync();
                var symbols = Recommender.GetRecommendedSymbolsAtPosition(model, position, _workspace);

                completions = symbols.Select(MakeAutoCompleteResponse);
            }

            return new ObjectResult(completions);
        }

        [HttpPost("rename")]
        public async Task<IActionResult> Rename([FromBody]RenameRequest request)
        {
            EnsureBufferUpdated(request);

            var response = new RenameResponse();

            var documentIds = _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName);

            var documentId = documentIds.FirstOrDefault();
            if (documentId != null)
            {
                var document = _workspace.CurrentSolution.GetDocument(documentId);
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column));

                var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position);
                Solution solution = _workspace.CurrentSolution;

                if (symbol != null)
                {
                    solution = await Renamer.RenameSymbolAsync(solution, symbol, request.RenameTo, _workspace.Options);
                }

                var changes = new List<ModifiedFileResponse>();

                var solutionChanges = solution.GetChanges(_workspace.CurrentSolution);

                foreach (var projectChange in solutionChanges.GetProjectChanges())
                {
                    foreach (var changedDocumentId in projectChange.GetChangedDocuments())
                    {
                        var changedDocument = solution.GetDocument(changedDocumentId);
                        var changedText = await changedDocument.GetTextAsync();
                        var modifiedFileResponse = new ModifiedFileResponse(changedDocument.FilePath, changedText.ToString());

                        changes.Add(modifiedFileResponse);
                    }
                }

                // Attempt to update the workspace
                if (_workspace.TryApplyChanges(solution))
                {
                    response.Changes = changes;
                }
            }

            return new ObjectResult(response);
        }

        private AutoCompleteResponse MakeAutoCompleteResponse(ISymbol symbol)
        {
            var response = new AutoCompleteResponse();
            response.CompletionText = symbol.Name;
            // TODO: Do something more intelligent here
            response.DisplayText = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            return response;
        }

        private void EnsureBufferUpdated(Request request)
        {
            foreach (var documentId in _workspace.CurrentSolution.GetDocumentIdsWithFilePath(request.FileName))
            {
                var buffer = Encoding.UTF8.GetBytes(request.Buffer);
                var sourceText = SourceText.From(new MemoryStream(buffer), encoding: Encoding.UTF8);
                _workspace.OnDocumentChanged(documentId, sourceText);
            }
        }

        private static QuickFix MakeQuickFix(Diagnostic diagnostic)
        {
            var span = diagnostic.Location.GetMappedLineSpan();
            var quickFix = new QuickFix();
            quickFix.FileName = span.Path;
            quickFix.Line = span.StartLinePosition.Line + 1;
            quickFix.Column = span.StartLinePosition.Character;
            quickFix.EndLine = span.EndLinePosition.Line + 1;
            quickFix.EndColumn = span.EndLinePosition.Character;
            quickFix.Text = diagnostic.GetMessage();
            quickFix.LogLevel = diagnostic.Severity.ToString();

            return quickFix;
        }
    }
}