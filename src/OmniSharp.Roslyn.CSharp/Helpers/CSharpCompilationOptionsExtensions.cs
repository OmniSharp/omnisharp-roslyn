using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;

namespace OmniSharp.Helpers
{
    public static class CSharpCompilationOptionsExtensions
    {
        private static Dictionary<string, ReportDiagnostic> defaultSuppressedDiagnostics = new Dictionary<string, ReportDiagnostic>
        {
            // ensure that specific warnings about assembly references are always suppressed
            { "CS1701", ReportDiagnostic.Suppress },
            { "CS1702", ReportDiagnostic.Suppress },
            { "CS1705", ReportDiagnostic.Suppress }
        };

        public static CSharpCompilationOptions WithDefaultSuppressedDiagnosticOptions(this CSharpCompilationOptions compilationOptions)
        {
            return compilationOptions.WithSpecificDiagnosticOptions(defaultSuppressedDiagnostics);
        }

        public static CSharpCompilationOptions WithDefaultSuppressedDiagnosticOptions(this CSharpCompilationOptions compilationOptions, IEnumerable<string> suppressedDiagnosticIds)
        {
            if (suppressedDiagnosticIds == null || !suppressedDiagnosticIds.Any()) return WithDefaultSuppressedDiagnosticOptions(compilationOptions);

            var suppressedDiagnostics = suppressedDiagnosticIds.ToDictionary(d => d, d => ReportDiagnostic.Suppress);
            foreach (var diagnostic in defaultSuppressedDiagnostics)
            {
                if (!suppressedDiagnostics.ContainsKey(diagnostic.Key))
                {
                    suppressedDiagnostics.Add(diagnostic.Key, diagnostic.Value);
                }
            }

            return compilationOptions.WithSpecificDiagnosticOptions(suppressedDiagnostics);
        }
    }
}
