#nullable enable

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.RoslynInternals.Completion
{
    public readonly struct OmniSharpCompletionOptions
    {
        public bool ShowItemsFromUnimportedNamespaces { get; }
        public bool ForceExpandedCompletionIndexCreation { get; }

        public OmniSharpCompletionOptions(bool ShowItemsFromUnimportedNamespaces, bool ForceExpandedCompletionIndexCreation)
        {
            this.ShowItemsFromUnimportedNamespaces = ShowItemsFromUnimportedNamespaces;
            this.ForceExpandedCompletionIndexCreation = ForceExpandedCompletionIndexCreation;
        }

        internal object ToRoslynOptions()
        {
            var type = RoslynReflection.GetType(RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.Completion.CompletionOptions");
            var defaultProperty = type.GetProperty(
                "Default", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                           System.Reflection.BindingFlags.Static);
            var value = defaultProperty?.GetValue(null);
            return RoslynReflection.CreateWithProperties(type, value,
                ("ShowItemsFromUnimportedNamespaces", ShowItemsFromUnimportedNamespaces),
                ("ForceExpandedCompletionIndexCreation", ForceExpandedCompletionIndexCreation),
                ("UpdateImportCompletionCacheInBackground", true));
        }
    }

    public static class RoslynCompletionService
    {
        public static async ValueTask<bool> ShouldTriggerCompletionAsync(
            CompletionService completionService, Document document, int caretPosition, CompletionTrigger trigger,
            ImmutableHashSet<string>? roles, OmniSharpCompletionOptions options, CancellationToken cancellationToken)
        {
            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var method = RoslynReflection.GetMethod(completionService.GetType(), "ShouldTriggerCompletion",
                m => !m.IsStatic && m.GetParameters().Length == 8);
            return RoslynReflection.Invoke<bool>(method, completionService,
                document.Project, document.Project.Services, text, caretPosition, trigger,
                options.ToRoslynOptions(), document.Project.Solution.Options, roles);
        }

        public static Task<CompletionList> GetCompletionsAsync(
            CompletionService completionService, Document document, int caretPosition, CompletionTrigger trigger,
            ImmutableHashSet<string>? roles, OmniSharpCompletionOptions options, CancellationToken cancellationToken)
        {
            var method = RoslynReflection.GetMethod(completionService.GetType(), "GetCompletionsAsync",
                m => !m.IsStatic && m.GetParameters().Length == 7 &&
                     m.GetParameters().Any(p => p.ParameterType.Name == "CompletionOptions"));
            return RoslynReflection.Invoke<Task<CompletionList>>(method, completionService,
                document, caretPosition, options.ToRoslynOptions(), document.Project.Solution.Options,
                trigger, roles, cancellationToken);
        }

        public static string? GetProviderName(this CompletionItem item)
            => (string?)RoslynReflection.GetPropertyValue(item, "ProviderName");
    }
}
