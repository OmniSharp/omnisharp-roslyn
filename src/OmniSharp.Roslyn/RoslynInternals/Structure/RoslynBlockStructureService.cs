#nullable enable

using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Roslyn.Reflection;

namespace OmniSharp.Roslyn.RoslynInternals.Structure
{
    public sealed class OmniSharpBlockStructure
    {
        public ImmutableArray<OmniSharpBlockSpan> Spans { get; }
        public OmniSharpBlockStructure(ImmutableArray<OmniSharpBlockSpan> spans) => Spans = spans;
    }

    public readonly struct OmniSharpBlockSpan
    {
        public string Type { get; }
        public bool IsCollapsible { get; }
        public TextSpan TextSpan { get; }
        public OmniSharpBlockSpan(string type, bool isCollapsible, TextSpan textSpan)
            => (Type, IsCollapsible, TextSpan) = (type, isCollapsible, textSpan);
    }

    public readonly struct OmniSharpBlockStructureOptions
    {
        public bool ShowBlockStructureGuidesForCommentsAndPreprocessorRegions { get; }
        public bool ShowOutliningForCommentsAndPreprocessorRegions { get; }
        public OmniSharpBlockStructureOptions(
            bool ShowBlockStructureGuidesForCommentsAndPreprocessorRegions,
            bool ShowOutliningForCommentsAndPreprocessorRegions)
        {
            this.ShowBlockStructureGuidesForCommentsAndPreprocessorRegions = ShowBlockStructureGuidesForCommentsAndPreprocessorRegions;
            this.ShowOutliningForCommentsAndPreprocessorRegions = ShowOutliningForCommentsAndPreprocessorRegions;
        }
    }

    public static class OmniSharpBlockTypes
    {
        public const string PreprocessorRegion = "PreprocessorRegion";
    }

    public static class RoslynBlockStructureService
    {
        public static async Task<OmniSharpBlockStructure?> GetBlockStructureAsync(
            Document document, OmniSharpBlockStructureOptions options, CancellationToken cancellationToken)
        {
            var service = RoslynReflection.GetRequiredLanguageService(
                document, RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.Structure.BlockStructureService");
            var optionsType = RoslynReflection.GetType(RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.Structure.BlockStructureOptions");
            var defaultProperty = optionsType.GetProperty(
                "Default", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var defaultField = optionsType.GetField(
                "Default", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var roslynOptions = RoslynReflection.CreateWithProperties(
                optionsType, defaultProperty?.GetValue(null) ?? defaultField?.GetValue(null),
                ("ShowBlockStructureGuidesForCommentsAndPreprocessorRegions", options.ShowBlockStructureGuidesForCommentsAndPreprocessorRegions),
                ("ShowOutliningForCommentsAndPreprocessorRegions", options.ShowOutliningForCommentsAndPreprocessorRegions));
            var method = RoslynReflection.GetMethod(service.GetType(), "GetBlockStructureAsync",
                m => !m.IsStatic && m.GetParameters().Length == 3);
            var result = await RoslynReflection.AwaitResultAsync(
                RoslynReflection.Invoke(method, service, document, roslynOptions, cancellationToken)).ConfigureAwait(false);
            if (result is null)
                return null;

            var spans = RoslynReflection.Enumerate(RoslynReflection.GetPropertyValue(result, "Spans"))
                .Select(span => new OmniSharpBlockSpan(
                    RoslynReflection.GetPropertyValue<string>(span, "Type"),
                    RoslynReflection.GetPropertyValue<bool>(span, "IsCollapsible"),
                    RoslynReflection.GetPropertyValue<TextSpan>(span, "TextSpan")))
                .ToImmutableArray();
            return new OmniSharpBlockStructure(spans);
        }
    }
}
