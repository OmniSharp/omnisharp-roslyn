using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Helpers
{
    public static class CompilationOptionsHelper
    {
        private static ImmutableDictionary<string, ReportDiagnostic> defaultSuppressedDiagnostics = new Dictionary<string, ReportDiagnostic>
        {
            // ensure that specific warnings about assembly references are always suppressed
            { "CS1701", ReportDiagnostic.Suppress },
            { "CS1702", ReportDiagnostic.Suppress },
            { "CS1705", ReportDiagnostic.Suppress }
        }.ToImmutableDictionary();

        public static ImmutableDictionary<string, ReportDiagnostic> GetDefaultSuppressedDiagnosticOptions()
        {
            return defaultSuppressedDiagnostics;
        }

        public static ImmutableDictionary<string, ReportDiagnostic> GetDefaultSuppressedDiagnosticOptions(IEnumerable<string> suppressedDiagnosticIds)
        {
            if (suppressedDiagnosticIds == null || !suppressedDiagnosticIds.Any()) return GetDefaultSuppressedDiagnosticOptions();

            var suppressedDiagnostics = suppressedDiagnosticIds.Distinct().ToDictionary(d => d, d => ReportDiagnostic.Suppress);
            foreach (var diagnostic in defaultSuppressedDiagnostics)
            {
                if (!suppressedDiagnostics.ContainsKey(diagnostic.Key))
                {
                    suppressedDiagnostics.Add(diagnostic.Key, diagnostic.Value);
                }
            }

            return suppressedDiagnostics.ToImmutableDictionary();
        }
    }
}
