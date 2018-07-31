using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using OmniSharp.Roslyn.CSharp.Services.Refactoring.V2;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [Shared]
    [Export(typeof(RulesetsForProjects))]
    public class RulesetsForProjects
    {
        private readonly ConcurrentDictionary<ProjectId, RuleSet> _rules = new ConcurrentDictionary<ProjectId, RuleSet>();

        public void AddOrUpdateRuleset(ProjectId projectId, RuleSet ruleset)
        {
            _rules.AddOrUpdate(projectId, ruleset, (_,__) => ruleset);
        }

        public IEnumerable<Diagnostic> ApplyRules(ProjectId projectId, IEnumerable<Diagnostic> originalDiagnostics)
        {
            var updated = originalDiagnostics
                .Select(item =>
                {
                    if (IsMatchingDiagnosticRule(projectId, item))
                    {
                        var newSeverity = GetNewSeverity(projectId, item);

                        if (newSeverity.suppressed)
                            return null;

                        return Diagnostic.Create(
                            item.Descriptor,
                            item.Location,
                            newSeverity.severity,
                            item.AdditionalLocations,
                            item.Properties,
                            new object[] { });
                    }
                    return item;
                })
                // Filter out suppressed diagnostics.
                .Where(x => x != null)
                .ToList();

            return updated;
        }

        private (DiagnosticSeverity severity, bool suppressed) GetNewSeverity(ProjectId projectId, Diagnostic item)
        {
            var rule = _rules[projectId].SpecificDiagnosticOptions.Single(x => x.Key == item.Id).Value;
            return (
                severity: ConvertReportSeverity(_rules[projectId].SpecificDiagnosticOptions.Single(x => x.Key == item.Id).Value, item.Severity),
                suppressed: rule == ReportDiagnostic.Suppress);
        }

        private bool IsMatchingDiagnosticRule(ProjectId projectId, Diagnostic item)
        {
            return _rules.ContainsKey(projectId) && _rules[projectId].SpecificDiagnosticOptions.Any(x => x.Key == item.Id);
        }

        private static DiagnosticSeverity ConvertReportSeverity(ReportDiagnostic reportDiagnostic, DiagnosticSeverity original)
        {
            switch(reportDiagnostic) {
                case ReportDiagnostic.Error:
                    return DiagnosticSeverity.Error;
                case ReportDiagnostic.Warn:
                    return DiagnosticSeverity.Warning;
                case ReportDiagnostic.Info:
                    return DiagnosticSeverity.Info;
                case ReportDiagnostic.Hidden:
                    return DiagnosticSeverity.Hidden;
                default:
                    return original;
            }
        }
    }
}
