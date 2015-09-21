using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(typeof(RequestHandler<FindUsagesRequest, QuickFixResponse>), LanguageNames.CSharp)]
    public class FindUsagesService : RequestHandler<FindUsagesRequest, QuickFixResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public FindUsagesService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<QuickFixResponse> Handle(FindUsagesRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            var response = new QuickFixResponse();
            if (document != null)
            {
                var locations = new List<Location>();
                var semanticModel = await document.GetSemanticModelAsync();
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var symbol = SymbolFinder.FindSymbolAtPosition(semanticModel, position, _workspace);
                var definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, _workspace.CurrentSolution);
                var usages = request.OnlyThisFile
                    ? await SymbolFinder.FindReferencesAsync(definition ?? symbol, _workspace.CurrentSolution, ImmutableHashSet.Create(document))
                    : await SymbolFinder.FindReferencesAsync(definition ?? symbol, _workspace.CurrentSolution);

                foreach (var usage in usages.Where(u => u.Definition.CanBeReferencedByName || (symbol as IMethodSymbol)?.MethodKind == MethodKind.Constructor))
                {
                    foreach (var location in usage.Locations)
                    {
                        locations.Add(location.Location);
                    }

                    if (!request.ExcludeDefinition)
                    {
                        var definitionLocations = usage.Definition.Locations
                            .Where(loc => loc.IsInSource && (!request.OnlyThisFile || loc.SourceTree.FilePath == request.FileName));

                        foreach (var location in definitionLocations)
                        {
                            locations.Add(location);
                        }
                    }
                }

                var quickFixTasks = locations.Distinct().Select(async l => await QuickFixHelper.GetQuickFix(_workspace, l));

                var quickFixes = await Task.WhenAll(quickFixTasks);
                response = new QuickFixResponse(quickFixes.Distinct()
                                                .OrderBy(q => q.FileName)
                                                .ThenBy(q => q.Line)
                                                .ThenBy(q => q.Column));
            }

            return response;
        }
    }
}
