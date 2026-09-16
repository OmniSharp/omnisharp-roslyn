#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Roslyn.Reflection;
using OmniSharp.Roslyn.RoslynInternals.Formatting;

namespace OmniSharp.Roslyn.RoslynInternals.Options
{
    public enum OmniSharpImplementTypeInsertionBehavior { WithOtherMembersOfTheSameKind, AtTheEnd }
    public enum OmniSharpImplementTypePropertyGenerationBehavior { PreferThrowingProperties, PreferAutoProperties }

    public struct OmniSharpImplementTypeOptions
    {
        public OmniSharpImplementTypeInsertionBehavior InsertionBehavior { get; set; }
        public OmniSharpImplementTypePropertyGenerationBehavior PropertyGenerationBehavior { get; set; }
    }

    public sealed class OmniSharpEditorConfigOptions
    {
        public OmniSharpLineFormattingOptions LineFormattingOptions { get; set; }
        public OmniSharpImplementTypeOptions ImplementTypeOptions { get; set; }
    }

    public static class RoslynSolutionAnalyzerConfigOptionsUpdater
    {
        public static bool UpdateOptions(Microsoft.CodeAnalysis.Workspace workspace, OmniSharpEditorConfigOptions options)
        {
            var values = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.OrdinalIgnoreCase);
            AddOption(RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Formatting.FormattingOptions2",
                "UseTabs", options.LineFormattingOptions.UseTabs);
            AddOption(RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Formatting.FormattingOptions2",
                "TabSize", options.LineFormattingOptions.TabSize);
            AddOption(RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Formatting.FormattingOptions2",
                "IndentationSize", options.LineFormattingOptions.IndentationSize);
            AddOption(RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Formatting.FormattingOptions2",
                "NewLine", options.LineFormattingOptions.NewLine);
            AddEnumOption("InsertionBehavior", options.ImplementTypeOptions.InsertionBehavior.ToString());
            AddEnumOption("PropertyGenerationBehavior", options.ImplementTypeOptions.PropertyGenerationBehavior.ToString());

            var dictionaryType = RoslynReflection.GetType(
                RoslynReflection.CodeAnalysisAssembly, "Microsoft.CodeAnalysis.Diagnostics.DictionaryAnalyzerConfigOptions");
            var dictionary = RoslynReflection.CreateInstance(dictionaryType, values.ToImmutable());
            var structuredType = RoslynReflection.GetType(
                RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Diagnostics.StructuredAnalyzerConfigOptions");
            var create = RoslynReflection.GetMethod(structuredType, "Create",
                m => m.IsStatic && m.GetParameters().Length >= 1 &&
                     m.GetParameters().Skip(1).All(p => p.IsOptional));
            var createArguments = Enumerable.Repeat<object>(Type.Missing, create.GetParameters().Length).ToArray();
            createArguments[0] = dictionary;
            var structured = RoslynReflection.Invoke(create, null, createArguments);

            var solution = workspace.CurrentSolution;
            var fallback = RoslynReflection.GetPropertyValue(solution, "FallbackAnalyzerOptions");
            var setItem = fallback.GetType().GetMethod("SetItem", new[] { typeof(string), structuredType })
                ?? throw new InvalidOperationException("Roslyn fallback analyzer options dictionary has an incompatible shape.");
            var updatedFallback = RoslynReflection.Invoke(setItem, fallback, LanguageNames.CSharp, structured);
            var withFallback = RoslynReflection.GetMethod(solution.GetType(), "WithFallbackAnalyzerOptions",
                m => !m.IsStatic && m.GetParameters().Length == 1);
            var updatedSolution = RoslynReflection.Invoke<Solution>(withFallback, solution, updatedFallback);
            return workspace.TryApplyChanges(updatedSolution);

            void AddEnumOption(string fieldName, string enumName)
            {
                var storageType = RoslynReflection.GetType(
                    RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.ImplementType.ImplementTypeOptionsStorage");
                var option = storageType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                    ?? throw new InvalidOperationException($"Roslyn implement-type option '{fieldName}' was not found.");
                var valueType = option.GetType().GetGenericArguments()[0];
                AddSerialized(option, Enum.Parse(valueType, enumName));
            }

            void AddOption(string assembly, string typeName, string fieldName, object value)
            {
                var type = RoslynReflection.GetType(assembly, typeName);
                var option = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                    ?? throw new InvalidOperationException($"Roslyn formatting option '{fieldName}' was not found.");
                AddSerialized(option, value);
            }

            void AddSerialized(object option, object value)
            {
                var definition = RoslynReflection.GetPropertyValue(option, "Definition");
                var configName = RoslynReflection.GetPropertyValue<string>(definition, "ConfigName");
                var serializer = RoslynReflection.GetPropertyValue(definition, "Serializer");
                var serialize = RoslynReflection.GetMethod(serializer.GetType(), "Serialize",
                    m => !m.IsStatic && m.GetParameters().Length == 1);
                values[configName] = RoslynReflection.Invoke<string>(serialize, serializer, value);
            }
        }
    }
}
