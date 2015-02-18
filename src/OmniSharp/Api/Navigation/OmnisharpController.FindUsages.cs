using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("findusages")]
        public async Task<QuickFixResponse> FindUsages(FindUsagesRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            var response = new QuickFixResponse();
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var symbol = SymbolFinder.FindSymbolAtPosition(semanticModel, position, _workspace);
                var definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, _workspace.CurrentSolution);
                var usages = request.OnlyThisFile
                    ? await SymbolFinder.FindReferencesAsync(definition ?? symbol, _workspace.CurrentSolution, ImmutableHashSet.Create(document))
                    : await SymbolFinder.FindReferencesAsync(definition ?? symbol, _workspace.CurrentSolution);

                var locations = new Dictionary<Location, HashSet<Project>>();
                foreach (var usage in usages.Where(u => u.Definition.CanBeReferencedByName || (symbol as IMethodSymbol)?.MethodKind == MethodKind.Constructor))
                {
                    foreach (var location in usage.Locations)
                    {
                        var first = locations.FirstOrDefault(l => l.Key.Equals(location.Location)).Key;
                        if (first == null)
                        {
                            locations[location.Location] = new HashSet<Project>();
                            first = location.Location;
                        }
                        else
                        {
                            int i = 0;
                        }
                        locations[first].Add(location.Document.Project);
                    }

                    if (!request.ExcludeThisLocation)
                    {
                        var definitionLocations = usage.Definition.Locations
                        .Where(loc => loc.IsInSource && (!request.OnlyThisFile || loc.SourceTree.FilePath == request.FileName));

                        foreach (var location in definitionLocations)
                        {
                            var first = locations.FirstOrDefault(l => l.Key.Equals(location)).Key;
                            if (first == null)
                            {
                                locations[location] = new HashSet<Project>();
                                first = location;
                            }
                            locations[first].Add(document.Project);
                        }
                    }
                }

                //there must be a better way to do this, but I can't think of it right now
                var quickFixTasks = locations.Select(async l =>
                {
                    var quickFix = await GetQuickFix(l.Key);
                    quickFix.Projects = l.Value.Select(project => project.Name).ToList();
                    return quickFix;
                });

                var quickFixes = await Task.WhenAll(quickFixTasks);

                response = new QuickFixResponse(quickFixes.OrderBy(q => q.FileName)
                                                            .ThenBy(q => q.Line)
                                                            .ThenBy(q => q.Column));
            }

            return response;
        }
    }
}
