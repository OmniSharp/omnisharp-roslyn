using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FindUsages;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.FindUsages, LanguageNames.CSharp)]
    public class FindUsagesService : IRequestHandler<FindUsagesRequest, QuickFixResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly ILogger<FindUsagesService> _logger;

        [ImportingConstructor]
        public FindUsagesService(OmniSharpWorkspace workspace, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _logger = loggerFactory.CreateLogger<FindUsagesService>();

        }

        public async Task<QuickFixResponse> Handle(FindUsagesRequest request)
        {
            // To produce complete list of usages for symbols in the document wait until all projects are loaded.
            var document = await _workspace.GetDocumentFromFullProjectModelAsync(request.FileName);
            if (document == null)
            {
                _logger.LogWarning($"No document found. File: {request.FileName}.");
                return new QuickFixResponse();
            }

            var semanticModel = await document.GetSemanticModelAsync();
            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, _workspace);
            if (symbol is null)
            {
                _logger.LogWarning($"No symbol found. File: {request.FileName}, Line: {request.Line}, Column: {request.Column}.");
                return new QuickFixResponse();
            }

            var definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, _workspace.CurrentSolution);
            var usages = request.OnlyThisFile
                ? await SymbolFinder.FindReferencesAsync(definition ?? symbol, _workspace.CurrentSolution, ImmutableHashSet.Create(document))
                : await SymbolFinder.FindReferencesAsync(definition ?? symbol, _workspace.CurrentSolution);
            var locations = usages.SelectMany(u => u.Locations).Select(l => l.Location).ToList();

            if (!request.ExcludeDefinition)
            {
                // always skip get/set methods of properties from the list of definition locations.
                var definitionLocations = usages.Select(u => u.Definition)
                    .Where(def => !(def is IMethodSymbol method && method.AssociatedSymbol is IPropertySymbol))
                    .SelectMany(def => def.Locations)
                    .Where(loc => loc.IsInSource && (!request.OnlyThisFile || loc.SourceTree.FilePath == request.FileName));

                locations.AddRange(definitionLocations);
            }

            var quickFixes = locations.Distinct().Select(l => l.GetQuickFix(_workspace));

            return new QuickFixResponse(quickFixes.Distinct()
                                            .OrderBy(q => q.FileName)
                                            .ThenBy(q => q.Line)
                                            .ThenBy(q => q.Column));

        }
    }
}
