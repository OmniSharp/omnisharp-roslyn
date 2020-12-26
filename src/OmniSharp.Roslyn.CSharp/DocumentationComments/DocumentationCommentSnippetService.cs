#nullable enable

using System;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;

namespace OmniSharp.Roslyn.DocumentationComments
{
    /// <summary>
    /// Proxy service for Microsoft.CodeAnalysis.DocumentationComments.IDocumentationCommentSnippetService.
    /// Implementation was based on the service as of this commit: 2834b74995bb66a7cb19cb09069c17812819afdc
    /// See: https://github.com/dotnet/roslyn/blob/2834b74995bb66a7cb19cb09069c17812819afdc/src/Features/Core/Portable/DocumentationComments/IDocumentationCommentSnippetService.cs
    /// </summary>
    public struct DocumentationCommentSnippetService
    {
        /// <summary>
        /// IDocumentationCommentService HostLanguageServices.GetRequiredService<IDocumentationCommentService>()
        /// </summary>
        private static readonly MethodInfo? s_getRequiredService;
        /// <summary>
        /// DocumentationCommentSnippet IDocumentationCommentService.GetDocumentationCommentSnippetOnCharacterTyped(SyntaxTree, SourceText, int, DocumentOptionSet, CancellationToken)
        /// </summary>
        private static readonly MethodInfo? s_getDocumentationCommentSnippetOnCharacterTyped;
        /// <summary>
        /// DocumentationCommentSnippet IDocumentationCommentService.GetDocumentationCommentSnippetOnEnterTyped(SyntaxTree, SourceText, int, DocumentOptionSet, CancellationToken)
        /// </summary>
        private static readonly MethodInfo? s_getDocumentationCommentSnippetOnEnterTyped;
        /// <summary>
        /// TextSpan DocumentationCommentSnippet.SpanToReplace
        /// </summary>
        private static readonly PropertyInfo? s_spanToReplace;
        /// <summary>
        /// string DocumentationCommentSnippet.SnippetText
        /// </summary>
        private static readonly PropertyInfo? s_snippetText;

        static DocumentationCommentSnippetService()
        {
            var iDocumentationCommentSnippetServiceType = typeof(CompletionItem).Assembly.GetType("Microsoft.CodeAnalysis.DocumentationComments.IDocumentationCommentSnippetService");
            s_getDocumentationCommentSnippetOnCharacterTyped = iDocumentationCommentSnippetServiceType?.GetMethod(nameof(GetDocumentationCommentSnippetOnCharacterTyped));
            s_getDocumentationCommentSnippetOnEnterTyped = iDocumentationCommentSnippetServiceType?.GetMethod(nameof(GetDocumentationCommentSnippetOnEnterTyped));

            var documentationCommentSnippet = typeof(CompletionItem).Assembly.GetType("Microsoft.CodeAnalysis.DocumentationComments.DocumentationCommentSnippet");
            s_spanToReplace = documentationCommentSnippet?.GetProperty(nameof(DocumentationCommentSnippet.SpanToReplace));
            s_snippetText = documentationCommentSnippet?.GetProperty(nameof(DocumentationCommentSnippet.SnippetText));

            if (iDocumentationCommentSnippetServiceType is object)
                s_getRequiredService = typeof(HostLanguageServices).GetMethod(nameof(HostLanguageServices.GetRequiredService))?.MakeGenericMethod(iDocumentationCommentSnippetServiceType);
        }

        public static DocumentationCommentSnippetService GetDocumentationCommentSnippetService(Document document)
        {
            if (s_getRequiredService is null) throw new InvalidOperationException("Could not find GetRequiredService<IDocumentationCommentService> factory.");
            var service = s_getRequiredService.Invoke(document.Project.LanguageServices, Array.Empty<object>());
            if (service is null) throw new InvalidOperationException("Could not retrieve an implementation of IDocumentationCommentService.");
            return new DocumentationCommentSnippetService(service);
        }

        private readonly object _underlying;

        private DocumentationCommentSnippetService(object underlying)
        {
            _underlying = underlying;
        }

        public DocumentationCommentSnippet? GetDocumentationCommentSnippetOnCharacterTyped(SyntaxTree syntaxTree, SourceText text, int position, DocumentOptionSet options, CancellationToken cancellationToken)
        {
            if (s_getDocumentationCommentSnippetOnCharacterTyped is null) throw new InvalidOperationException("Could not find IDocumentationCommentService.GetDocumentationCommentSnippetOnCharacterTyped(SyntaxTree, SourceText, int, DocumentOptionSet, CancellationToken).");
            var originalSnippet = s_getDocumentationCommentSnippetOnCharacterTyped.Invoke(_underlying, new object[] { syntaxTree, text, position, options, cancellationToken });
            return ConvertSnippet(originalSnippet);
        }

        public DocumentationCommentSnippet? GetDocumentationCommentSnippetOnEnterTyped(SyntaxTree syntaxTree, SourceText text, int position, DocumentOptionSet options, CancellationToken cancellationToken)
        {
            if (s_getDocumentationCommentSnippetOnEnterTyped is null) throw new InvalidOperationException("Could not find IDocumentationCommentService.GetDocumentationCommentSnippetOnEnterTyped(SyntaxTree, SourceText, int, DocumentOptionSet, CancellationToken).");
            var originalSnippet = s_getDocumentationCommentSnippetOnEnterTyped.Invoke(_underlying, new object[] { syntaxTree, text, position, options, cancellationToken });
            return ConvertSnippet(originalSnippet);
        }

        private static DocumentationCommentSnippet? ConvertSnippet(object? originalSnippet)
        {
            switch (originalSnippet)
            {
                case null:
                    return null;
                default:
                    if (s_spanToReplace is null) throw new InvalidOperationException("Could not find DocumentationCommentSnippet.SpanToReplace.");
                    if (s_snippetText is null) throw new InvalidOperationException("Could not find DocumentationCommentSnippet.SnippetText.");
                    return new DocumentationCommentSnippet((TextSpan)s_spanToReplace.GetValue(originalSnippet)!, (string)s_snippetText.GetValue(originalSnippet)!);
            }
        }
    }

    public struct DocumentationCommentSnippet
    {
        public TextSpan SpanToReplace { get; }
        public string SnippetText { get; }

        public DocumentationCommentSnippet(TextSpan spanToReplace, string snippetText)
        {
            SpanToReplace = spanToReplace;
            SnippetText = snippetText;
        }
    }

    public static class WorkspaceExtensions
    {
        public static DocumentationCommentSnippetService GetDocumentationCommentSnippetService(this Document document)
            => DocumentationCommentSnippetService.GetDocumentationCommentSnippetService(document);
    }
}
