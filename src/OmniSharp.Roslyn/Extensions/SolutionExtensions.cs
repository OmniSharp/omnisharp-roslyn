using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Models;
using OmniSharp.Models.v1.SourceGeneratedFile;

#nullable enable

namespace OmniSharp.Extensions
{
    public static class SolutionExtensions
    {
        public static async Task<QuickFixResponse> FindSymbols(this Solution solution,
            string pattern,
            string projectFileExtension,
            int maxItemsToReturn,
            SymbolFilter symbolFilter = SymbolFilter.TypeAndMember)
        {
            var projects = solution.Projects.Where(p =>
                (p.FilePath?.EndsWith(projectFileExtension, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Name?.EndsWith(projectFileExtension, StringComparison.OrdinalIgnoreCase) ?? false));

            var symbolLocations = new List<QuickFix>();

            foreach (var project in projects)
            {
                var symbols = !string.IsNullOrEmpty(pattern) ?
                    await SymbolFinder.FindSourceDeclarationsWithPatternAsync(project, pattern, symbolFilter) :
                    await SymbolFinder.FindSourceDeclarationsAsync(project, candidate => true, symbolFilter);

                foreach (var symbol in symbols)
                {
                    // for partial methods, pick the one with body
                    var s = symbol;
                    if (s is IMethodSymbol method)
                    {
                        s = method.PartialImplementationPart ?? symbol;
                    }

                    foreach (var location in s.Locations)
                    {
                        symbolLocations.Add(ConvertSymbol(solution, symbol, location));
                    }

                    if (ShouldStopSearching(maxItemsToReturn, symbolLocations))
                    {
                        break;
                    }
                }

                if (ShouldStopSearching(maxItemsToReturn, symbolLocations))
                {
                    break;
                }
            }

            return new QuickFixResponse(symbolLocations.Distinct().ToList());
        }

        private static bool ShouldStopSearching(int maxItemsToReturn, List<QuickFix> symbolLocations)
        {
            return maxItemsToReturn > 0 && symbolLocations.Count >= maxItemsToReturn;
        }

        private static QuickFix ConvertSymbol(Solution solution, ISymbol symbol, Location location)
        {
            var lineSpan = location.GetLineSpan();
            var path = lineSpan.Path;
            var projects = solution.GetDocumentIdsWithFilePath(path)
                                    .Select(documentId => solution.GetProject(documentId.ProjectId)!.Name)
                                    .ToArray();

            var format = SymbolDisplayFormat.MinimallyQualifiedFormat;
            format = format.WithMemberOptions(format.MemberOptions
                                              ^ SymbolDisplayMemberOptions.IncludeContainingType
                                              ^ SymbolDisplayMemberOptions.IncludeType);

            format = format.WithKindOptions(SymbolDisplayKindOptions.None);

            return new SymbolLocation
            {
                Text = symbol.ToDisplayString(format),
                Kind = symbol.GetKind(),
                FileName = path,
                Line = lineSpan.StartLinePosition.Line,
                Column = lineSpan.StartLinePosition.Character,
                EndLine = lineSpan.EndLinePosition.Line,
                EndColumn = lineSpan.EndLinePosition.Character,
                Projects = projects,
                ContainingSymbolName = symbol.ContainingSymbol?.Name ?? "",
                GeneratedFileInfo = GetSourceGeneratedFileInfo(solution, location),
            };
        }

        internal static SourceGeneratedFileInfo? GetSourceGeneratedFileInfo(this Solution solution, Location location)
        {
            Debug.Assert(location.IsInSource);
            var document = solution.GetDocument(location.SourceTree);
            if (document is not SourceGeneratedDocument)
            {
                return null;
            }

            return new SourceGeneratedFileInfo
            {
                ProjectGuid = document.Project.Id.Id,
                DocumentGuid = document.Id.Id
            };
        }
    }
}
