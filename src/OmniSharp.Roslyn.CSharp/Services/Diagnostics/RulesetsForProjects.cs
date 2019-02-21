using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Roslyn.CSharp.Services.Diagnostics
{
    [Shared]
    [Export(typeof(RulesetsForProjects))]
    public class RulesetsForProjects
    {
        private readonly ConcurrentDictionary<ProjectId, RuleSet> _rules = new ConcurrentDictionary<ProjectId, RuleSet>();
        public ImmutableDictionary<string, ReportDiagnostic> GetRules(ProjectId projectId)
        {
            if (!_rules.ContainsKey(projectId))
                return ImmutableDictionary<string, ReportDiagnostic>.Empty;

            return _rules[projectId].SpecificDiagnosticOptions;
        }

        public CompilationOptions BuildCompilationOptionsWithCurrentRules(Project project)
        {
            if (!_rules.ContainsKey(project.Id))
                return project.CompilationOptions;

            var existingRules = project.CompilationOptions.SpecificDiagnosticOptions;
            return project.CompilationOptions.WithSpecificDiagnosticOptions(existingRules.Concat(GetRules(project.Id)));
        }

        public void AddOrUpdateRuleset(ProjectId projectId, RuleSet ruleset)
        {
            _rules.AddOrUpdate(projectId, ruleset, (_,__) => ruleset);
        }
    }
}
