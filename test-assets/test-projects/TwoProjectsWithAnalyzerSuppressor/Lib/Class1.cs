using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Lib
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MySuppressor : DiagnosticSuppressor
    {
       public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = new[]
       {
           new SuppressionDescriptor("SUPP3662", "SA1200", "Dummy suppression")
       }.ToImmutableArray();

       public override void ReportSuppressions(SuppressionAnalysisContext context)
       {
           foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
           {
                context.ReportSuppression(Suppression.Create(SupportedSuppressions[0], diagnostic));
           }
       }
    }
}
