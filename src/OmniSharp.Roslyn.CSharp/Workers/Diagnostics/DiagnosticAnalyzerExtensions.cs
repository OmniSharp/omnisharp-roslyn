using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn.CSharp.Workers.Diagnostics
{
    public static class DiagnosticAnalyzerExtensions
    {
        private static Lazy<MethodInfo> TryGetMappedOptionsMethod { get; }

        static DiagnosticAnalyzerExtensions()
        {
            TryGetMappedOptionsMethod = new Lazy<Assembly>(() => Assembly.Load(new AssemblyName("Microsoft.CodeAnalysis.Features")))
                .LazyGetType("Microsoft.CodeAnalysis.Diagnostics.IDEDiagnosticIdToOptionMappingHelper")
                .LazyGetMethod("TryGetMappedOptions", BindingFlags.Static | BindingFlags.Public);
        }

        /// <summary>
        /// Determines whether this analyzer will generate diagnostics of at least a minimum severity for any document within the project.
        /// </summary>
        public static async Task<bool> HasMinimumSeverityAsync(
            this DiagnosticAnalyzer analyzer,
            Project project,
            Compilation compilation,
            ReportDiagnostic minimumSeverity,
            CancellationToken cancellationToken)
        {
            if (compilation is null)
            {
                return false;
            }

            foreach (var document in project.Documents)
            {
                // Is the document formattable?
                if (document.FilePath is null)
                {
                    continue;
                }

                var options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);

                if (analyzer.HasMinimumSeverity(document, project.AnalyzerOptions, options, minimumSeverity, compilation))
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// Determines whether this analyzer will generate diagnostics of at least a minimum severity for this document.
        /// </summary>
        public static bool HasMinimumSeverity(
            this DiagnosticAnalyzer analyzer,
            Document document,
            AnalyzerOptions analyzerOptions,
            OptionSet options,
            ReportDiagnostic minimumSeverity,
            Compilation compilation)
        {
            if (!document.TryGetSyntaxTree(out var tree))
            {
                return false;
            }

            var severity = ReportDiagnostic.Suppress;

            foreach (var descriptor in analyzer.SupportedDiagnostics)
            {
                if (severity <= minimumSeverity)
                {
                    return true;
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

            return severity <= minimumSeverity;

            static bool TryGetSeverityFromCodeStyleOption(
                DiagnosticDescriptor descriptor,
                Compilation compilation,
                OptionSet options,
                out ReportDiagnostic severity)
            {
                severity = ReportDiagnostic.Suppress;

                var parameters = new object[] { descriptor.Id, compilation.Language, null };
                var result = (bool)(TryGetMappedOptionsMethod.InvokeStatic(parameters) ?? false);

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
