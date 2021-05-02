using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CodeFixes;

namespace OmniSharp.Roslyn.CSharp.Helpers
{
    internal static class CodeFixProviderExtensions
    {
        // http://sourceroslyn.io/#Microsoft.VisualStudio.LanguageServices.CSharp/LanguageService/CSharpCodeCleanupFixer.cs,d9a375db0f1e430e,references
        // CS8019 isn't directly used (via roslyn) but has an analyzer that report different diagnostic based on CS8019 to improve user experience.
        private static readonly Dictionary<string, string> _customDiagVsFixMap = new Dictionary<string, string>
        {
            { "CS8019", "RemoveUnnecessaryImportsFixable" }
        };

        // Theres specific filterings between what is shown and what is fixed because of some custom mappings
        // between diagnostics and their fixers. We dont want to show text 'RemoveUnnecessaryImportsFixable: ...'
        // but instead 'CS8019: ...' where actual fixer is RemoveUnnecessaryImportsFixable behind the scenes.
        public static bool HasFixForId(this CodeFixProvider provider, string diagnosticId)
        {
            return provider.FixableDiagnosticIds.Any(id => id == diagnosticId) && !_customDiagVsFixMap.ContainsKey(diagnosticId) || HasMappedFixAvailable(diagnosticId, provider);
        }

        private static bool HasMappedFixAvailable(string diagnosticId, CodeFixProvider provider)
        {
            return (_customDiagVsFixMap.ContainsKey(diagnosticId) && provider.FixableDiagnosticIds.Any(id => id == _customDiagVsFixMap[diagnosticId]));
        }
    }
}
