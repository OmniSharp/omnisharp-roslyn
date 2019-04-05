using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using OmniSharp.MSBuild.ProjectFile;

namespace OmniSharp.MSBuild
{
    internal static class RulesetUtils
    {
        internal static void UpdateProjectWithRulesets(Workspace workspace, Project project, ImmutableDictionary<string, ReportDiagnostic> newRules)
        {
            var existingRules = project.CompilationOptions.SpecificDiagnosticOptions;
            var distinctRulesWithProjectSpecificRules = newRules.Concat(existingRules.Where(x => !newRules.Keys.Contains(x.Key)));

            var updatedProject = project.WithCompilationOptions(
                project.CompilationOptions.WithSpecificDiagnosticOptions(distinctRulesWithProjectSpecificRules)
            );

            var updatedSolution = workspace
                .CurrentSolution
                .WithProjectCompilationOptions(
                    project.Id, project.CompilationOptions.WithSpecificDiagnosticOptions(distinctRulesWithProjectSpecificRules));

            workspace.TryApplyChanges(updatedSolution);
        }
    }
}