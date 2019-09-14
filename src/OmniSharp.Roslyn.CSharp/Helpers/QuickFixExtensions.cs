using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;

namespace OmniSharp.Helpers
{
    internal static class QuickFixExtensions
    {
        internal static void Add(this ICollection<QuickFix> quickFixes, ISymbol symbol, OmniSharpWorkspace workspace)
        {
            foreach (var location in symbol.Locations)
            {
                quickFixes.Add(location, workspace);
            }
        }

        internal static void Add(this ICollection<QuickFix> quickFixes, Location location, OmniSharpWorkspace workspace)
        {
            if (location.IsInSource)
            {
                var quickFix = location.GetQuickFix(workspace);
                quickFixes.Add(quickFix);
            }
        }

        internal static void AddRange(this ICollection<QuickFix> quickFixes, IEnumerable<ISymbol> symbols, OmniSharpWorkspace workspace)
        {
            foreach (var symbol in symbols)
            {
                foreach (var location in symbol.Locations)
                {
                    quickFixes.Add(location, workspace);
                }
            }
        }
    }
}
