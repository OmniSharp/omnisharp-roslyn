#nullable enable

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.RoslynInternals.InlayHints
{
    public readonly struct OmniSharpInlineParameterHintsOptions
    {
        public bool EnabledForParameters { get; init; }
        public bool ForLiteralParameters { get; init; }
        public bool ForIndexerParameters { get; init; }
        public bool ForObjectCreationParameters { get; init; }
        public bool ForOtherParameters { get; init; }
        public bool SuppressForParametersThatDifferOnlyBySuffix { get; init; }
        public bool SuppressForParametersThatMatchMethodIntent { get; init; }
        public bool SuppressForParametersThatMatchArgumentName { get; init; }
    }

    public readonly struct OmniSharpInlineTypeHintsOptions
    {
        public bool EnabledForTypes { get; init; }
        public bool ForImplicitVariableTypes { get; init; }
        public bool ForLambdaParameterTypes { get; init; }
        public bool ForImplicitObjectCreation { get; init; }
    }

    public readonly struct OmniSharpInlineHintsOptions
    {
        public OmniSharpInlineParameterHintsOptions ParameterOptions { get; init; }
        public OmniSharpInlineTypeHintsOptions TypeOptions { get; init; }

        internal object ToRoslynOptions()
        {
            var parameterType = RoslynReflection.GetType(RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.InlineHints.InlineParameterHintsOptions");
            var parameter = RoslynReflection.CreateWithProperties(parameterType, null,
                ("EnabledForParameters", ParameterOptions.EnabledForParameters),
                ("ForLiteralParameters", ParameterOptions.ForLiteralParameters),
                ("ForIndexerParameters", ParameterOptions.ForIndexerParameters),
                ("ForObjectCreationParameters", ParameterOptions.ForObjectCreationParameters),
                ("ForOtherParameters", ParameterOptions.ForOtherParameters),
                ("SuppressForParametersThatDifferOnlyBySuffix", ParameterOptions.SuppressForParametersThatDifferOnlyBySuffix),
                ("SuppressForParametersThatMatchMethodIntent", ParameterOptions.SuppressForParametersThatMatchMethodIntent),
                ("SuppressForParametersThatMatchArgumentName", ParameterOptions.SuppressForParametersThatMatchArgumentName));
            var typeType = RoslynReflection.GetType(RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.InlineHints.InlineTypeHintsOptions");
            var type = RoslynReflection.CreateWithProperties(typeType, null,
                ("EnabledForTypes", TypeOptions.EnabledForTypes),
                ("ForImplicitVariableTypes", TypeOptions.ForImplicitVariableTypes),
                ("ForLambdaParameterTypes", TypeOptions.ForLambdaParameterTypes),
                ("ForImplicitObjectCreation", TypeOptions.ForImplicitObjectCreation));
            var optionsType = RoslynReflection.GetType(RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.InlineHints.InlineHintsOptions");
            return RoslynReflection.CreateWithProperties(optionsType, null, ("ParameterOptions", parameter), ("TypeOptions", type));
        }
    }

    public readonly struct OmniSharpInlineHint
    {
        private readonly object _value;
        public TextSpan Span { get; }
        public double Ranking { get; }
        public ImmutableArray<TaggedText> DisplayParts { get; }
        public TextChange? ReplacementTextChange { get; }

        internal OmniSharpInlineHint(object value)
        {
            _value = value;
            Span = (TextSpan)RoslynReflection.GetFieldValue(value, "Span");
            Ranking = (double)RoslynReflection.GetFieldValue(value, "Ranking");
            DisplayParts = (ImmutableArray<TaggedText>)RoslynReflection.GetFieldValue(value, "DisplayParts");
            ReplacementTextChange = (TextChange?)RoslynReflection.GetFieldValue(value, "ReplacementTextChange");
        }

        public Task<ImmutableArray<TaggedText>> GetDescriptionAsync(Document document, CancellationToken cancellationToken)
        {
            var method = RoslynReflection.GetMethod(_value.GetType(), "GetDescriptionAsync",
                m => !m.IsStatic && m.GetParameters().Length == 2);
            return RoslynReflection.Invoke<Task<ImmutableArray<TaggedText>>>(method, _value, document, cancellationToken);
        }
    }

    public static class RoslynInlineHintsService
    {
        public static async Task<ImmutableArray<OmniSharpInlineHint>> GetInlineHintsAsync(
            Document document, TextSpan textSpan, OmniSharpInlineHintsOptions options, CancellationToken cancellationToken)
        {
            var service = RoslynReflection.GetRequiredLanguageService(
                document, RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.InlineHints.IInlineHintsService");
            var method = RoslynReflection.GetMethod(service.GetType(), "GetInlineHintsAsync",
                m => !m.IsStatic && m.GetParameters().Length == 5);
            var result = await RoslynReflection.AwaitResultAsync(
                RoslynReflection.Invoke(method, service, document, textSpan, options.ToRoslynOptions(), false, cancellationToken))
                .ConfigureAwait(false);
            return RoslynReflection.Enumerate(result).Select(h => new OmniSharpInlineHint(h)).ToImmutableArray();
        }
    }
}
