using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    public static class DiagnosticAnalyzerExtensions
    {
        private static MethodInfo TryGetMappedOptionsMethod { get; }

        static DiagnosticAnalyzerExtensions()
        {
            TryGetMappedOptionsMethod = Assembly.Load(new AssemblyName("Microsoft.CodeAnalysis.Features"))
                .GetType("Microsoft.CodeAnalysis.Diagnostics.IDEDiagnosticIdToOptionMappingHelper")
                .GetMethod("TryGetMappedOptions", BindingFlags.Static | BindingFlags.Public);
        }
        /// <summary>
        /// Get the highest possible severity for any formattable document in the project.
        /// </summary>
        public static async Task<ReportDiagnostic> GetSeverityAsync(
            this DiagnosticAnalyzer analyzer,
            Project project,
            Compilation compilation,
            CancellationToken cancellationToken)
        {
            var severity = ReportDiagnostic.Suppress;
            if (compilation is null)
            {
                return severity;
            }

            foreach (var document in project.Documents)
            {
                // Is the document formattable?
                if (document.FilePath is null)
                {
                    continue;
                }

                var options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);

                var documentSeverity = analyzer.GetSeverity(document, project.AnalyzerOptions, options, compilation);
                if (documentSeverity < severity)
                {
                    severity = documentSeverity;
                }
            }

            return severity;
        }

        public static ReportDiagnostic FromSeverity(this DiagnosticSeverity diagnosticSeverity)
        {
            return diagnosticSeverity switch
            {
                DiagnosticSeverity.Error => ReportDiagnostic.Error,
                DiagnosticSeverity.Warning => ReportDiagnostic.Warn,
                DiagnosticSeverity.Info => ReportDiagnostic.Info,
                _ => ReportDiagnostic.Hidden,
            };
        }

        public static DiagnosticSeverity ToSeverity(this ReportDiagnostic reportDiagnostic)
        {
            return reportDiagnostic switch
            {
                ReportDiagnostic.Error => DiagnosticSeverity.Error,
                ReportDiagnostic.Warn => DiagnosticSeverity.Warning,
                ReportDiagnostic.Info => DiagnosticSeverity.Info,
                _ => DiagnosticSeverity.Hidden
            };
        }

        public static ReportDiagnostic GetSeverity(
            this DiagnosticAnalyzer analyzer,
            Document document,
            AnalyzerOptions analyzerOptions,
            OptionSet options,
            Compilation compilation)
        {
            var severity = ReportDiagnostic.Suppress;

            if (!document.TryGetSyntaxTree(out var tree))
            {
                return severity;
            }

            foreach (var descriptor in analyzer.SupportedDiagnostics)
            {
                if (severity == ReportDiagnostic.Error)
                {
                    break;
                }

                if (analyzerOptions.TryGetSeverityFromConfiguration(tree, compilation, descriptor, out var reportDiagnostic))
                {
                    var configuredSeverity = reportDiagnostic;
                    if (configuredSeverity < severity)
                    {
                        severity = configuredSeverity;
                    }

                    continue;
                }

                if (TryGetSeverityFromCodeStyleOption(descriptor, compilation, options, out var codeStyleSeverity))
                {
                    if (codeStyleSeverity < severity)
                    {
                        severity = codeStyleSeverity;
                    }

                    continue;
                }

                var defaultSeverity = FromSeverity(descriptor.DefaultSeverity);
                if (defaultSeverity < severity)
                {
                    severity = defaultSeverity;
                }
            }

            return severity;

            static bool TryGetSeverityFromCodeStyleOption(
                DiagnosticDescriptor descriptor,
                Compilation compilation,
                OptionSet options,
                out ReportDiagnostic severity)
            {
                severity = ReportDiagnostic.Suppress;

                var parameters = new object[] { descriptor.Id, compilation.Language, null };
                var result = (bool)(TryGetMappedOptionsMethod.Invoke(null, parameters) ?? false);

                if (!result)
                {
                    return false;
                }

                var codeStyleOptions = (IEnumerable)parameters[2]!;
                foreach (var codeStyleOptionObj in codeStyleOptions)
                {
                    var codeStyleOption = (IOption)codeStyleOptionObj!;
                    var option = options.GetOption(new OptionKey(codeStyleOption, codeStyleOption.IsPerLanguage ? compilation.Language : null));
                    if (option is null)
                    {
                        continue;
                    }

                    var notificationProperty = option.GetType().GetProperty("Notification");
                    if (notificationProperty is null)
                    {
                        continue;
                    }

                    var notification = notificationProperty.GetValue(option);
                    var reportDiagnosticValue = notification?.GetType().GetProperty("Severity")?.GetValue(notification);
                    if (reportDiagnosticValue is null)
                    {
                        continue;
                    }

                    var codeStyleSeverity = (ReportDiagnostic)reportDiagnosticValue;
                    if (codeStyleSeverity < severity)
                    {
                        severity = codeStyleSeverity;
                    }
                }

                return true;
            }
        }
    }
}
