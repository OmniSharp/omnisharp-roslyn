using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Models;

namespace OmniSharp.Helpers
{
    internal static class QuickFixExtensions
    {
        internal static async Task AddQuickFix(this ICollection<QuickFix> quickFixes, OmniSharpWorkspace workspace, Location location)
        {
            if (location.IsInSource)
            {
                var quickFix = await QuickFixHelper.GetQuickFix(workspace, location);
                quickFixes.Add(quickFix);
            }
        }

        internal static async Task AddQuickFixes(this ICollection<QuickFix> quickFixes, OmniSharpWorkspace workspace, IEnumerable<ISymbol> symbols)
        {
            foreach (var symbol in symbols)
            {
                foreach (var location in symbol.Locations)
                {
                    await AddQuickFix(quickFixes, workspace, location);
                }
            }
        }
    }
}
