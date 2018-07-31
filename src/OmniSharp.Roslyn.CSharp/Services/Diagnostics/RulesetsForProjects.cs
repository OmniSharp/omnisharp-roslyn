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
                    if (_rules.ContainsKey(projectId) && _rules[projectId].SpecificDiagnosticOptions.Any(x => x.Key == item.Id))
                    {
                        var newSeverity = _rules[projectId].SpecificDiagnosticOptions.Single(x => x.Key == item.Id).Value;

                        return Diagnostic.Create(
                            item.Descriptor,
                            item.Location,
                            ConvertReportSeverity(newSeverity),
                            item.AdditionalLocations,
                            item.Properties,
                            new object[] {});
                    }
                    return item;
                });

            return updated;
        }

        private static DiagnosticSeverity ConvertReportSeverity(ReportDiagnostic reportDiagnostic)
        {
            switch(reportDiagnostic) {
                case ReportDiagnostic.Error:
                    return DiagnosticSeverity.Error;
                case ReportDiagnostic.Warn:
                    return DiagnosticSeverity.Warning;
                case ReportDiagnostic.Info:
                    return DiagnosticSeverity.Info;
                default:
                    return DiagnosticSeverity.Hidden;
            }
        }
    }
}
