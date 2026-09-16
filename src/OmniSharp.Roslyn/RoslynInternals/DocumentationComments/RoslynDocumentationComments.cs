#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Roslyn.Reflection;
using OmniSharp.Roslyn.RoslynInternals.Formatting;

namespace OmniSharp.Roslyn.RoslynInternals.DocumentationComments
{
    public readonly struct OmniSharpDocumentationCommentOptionsWrapper
    {
        internal object UnderlyingObject { get; }

        public OmniSharpDocumentationCommentOptionsWrapper(
            bool autoXmlDocCommentGeneration, OmniSharpLineFormattingOptions lineFormattingOptions)
        {
            var lineType = RoslynReflection.GetType(
                RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.Formatting.LineFormattingOptions");
            var line = RoslynReflection.CreateWithProperties(lineType, null,
                ("UseTabs", lineFormattingOptions.UseTabs), ("TabSize", lineFormattingOptions.TabSize),
                ("IndentationSize", lineFormattingOptions.IndentationSize), ("NewLine", lineFormattingOptions.NewLine));
            var type = RoslynReflection.GetType(
                RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.DocumentationComments.DocumentationCommentOptions");
            UnderlyingObject = RoslynReflection.CreateWithProperties(
                type, null, ("LineFormatting", line), ("AutoXmlDocCommentGeneration", autoXmlDocCommentGeneration));
        }

        private OmniSharpDocumentationCommentOptionsWrapper(object value) => UnderlyingObject = value;

        public static async ValueTask<OmniSharpDocumentationCommentOptionsWrapper> FromDocumentAsync(
            Document document, bool autoXmlDocCommentGeneration, CancellationToken cancellationToken)
        {
            var formatting = await OmniSharpSyntaxFormattingOptionsWrapper
                .FromDocumentAsync(document, default, cancellationToken).ConfigureAwait(false);
            var line = RoslynReflection.GetPropertyValue(formatting.UnderlyingObject, "LineFormatting");
            var type = RoslynReflection.GetType(
                RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.DocumentationComments.DocumentationCommentOptions");
            return new OmniSharpDocumentationCommentOptionsWrapper(
                RoslynReflection.CreateWithProperties(
                    type, null, ("LineFormatting", line), ("AutoXmlDocCommentGeneration", autoXmlDocCommentGeneration)));
        }
    }

    public sealed class OmniSharpDocumentationCommentSnippet
    {
        public TextSpan SpanToReplace { get; }
        public string SnippetText { get; }
        public int CaretOffset { get; }
        internal OmniSharpDocumentationCommentSnippet(object value)
        {
            SpanToReplace = RoslynReflection.GetPropertyValue<TextSpan>(value, "SpanToReplace");
            SnippetText = RoslynReflection.GetPropertyValue<string>(value, "SnippetText");
            CaretOffset = RoslynReflection.GetPropertyValue<int>(value, "CaretOffset");
        }
    }

    public static class RoslynDocumentationCommentsSnippetService
    {
        public static OmniSharpDocumentationCommentSnippet? GetDocumentationCommentSnippetOnCharacterTyped(
            Document document, SyntaxTree syntaxTree, SourceText text, int position,
            OmniSharpDocumentationCommentOptionsWrapper options, CancellationToken cancellationToken)
            => Get(document, position, options, cancellationToken, "GetDocumentationCommentSnippetOnCharacterTyped");

        public static OmniSharpDocumentationCommentSnippet? GetDocumentationCommentSnippetOnEnterTyped(
            Document document, SyntaxTree syntaxTree, SourceText text, int position,
            OmniSharpDocumentationCommentOptionsWrapper options, CancellationToken cancellationToken)
            => Get(document, position, options, cancellationToken, "GetDocumentationCommentSnippetOnEnterTyped");

        private static OmniSharpDocumentationCommentSnippet? Get(
            Document document, int position, OmniSharpDocumentationCommentOptionsWrapper options,
            CancellationToken cancellationToken, string methodName)
        {
            var service = RoslynReflection.GetRequiredLanguageService(
                document, RoslynReflection.FeaturesAssembly,
                "Microsoft.CodeAnalysis.DocumentationComments.IDocumentationCommentSnippetService");
            var parsedType = RoslynReflection.GetType(RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.ParsedDocument");
            var create = RoslynReflection.GetMethod(parsedType, "CreateSynchronously",
                m => m.IsStatic && m.GetParameters().Length == 2);
            var parsed = RoslynReflection.Invoke(create, null, document, cancellationToken);
            var method = RoslynReflection.GetMethod(service.GetType(), methodName,
                m => !m.IsStatic && m.GetParameters().Length == 4);
            var result = RoslynReflection.Invoke(method, service, parsed, position, options.UnderlyingObject, cancellationToken);
            return result is null ? null : new OmniSharpDocumentationCommentSnippet(result);
        }
    }

    public static class RoslynDocCommentConverter
    {
        public static SyntaxNode ConvertToRegularComments(
            SyntaxNode node, Project project, CancellationToken cancellationToken)
        {
            var serviceType = RoslynReflection.GetType(
                RoslynReflection.FeaturesAssembly,
                "Microsoft.CodeAnalysis.DocumentationComments.IDocumentationCommentFormattingService");
            var services = project.Services;
            var getService = services.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(m => m.Name == "GetService" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
            var service = RoslynReflection.Invoke(getService.MakeGenericMethod(serviceType), services)
                ?? throw new InvalidOperationException("Roslyn documentation comment formatting service is unavailable.");
            var converter = RoslynReflection.GetType(
                RoslynReflection.CSharpFeaturesAssembly,
                "Microsoft.CodeAnalysis.CSharp.DocumentationComments.DocCommentConverter");
            var method = RoslynReflection.GetMethod(converter, "ConvertToRegularComments",
                m => m.IsStatic && m.GetParameters().Length == 3);
            return RoslynReflection.Invoke<SyntaxNode>(method, null, node, service, cancellationToken);
        }
    }
}
