using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Formatting;

namespace OmniSharp
{
    public class Formatting
    {
        public static async Task<IEnumerable<TextChange>> GetFormattingChangesAfterKeystroke(Workspace workspace, OptionSet options, Document document, int position)
        {
            var tree = await document.GetSyntaxTreeAsync();
            var target = FindFormatTarget(tree, position);

            if (target == null)
            {
                return Enumerable.Empty<TextChange>();
            }

            // Instead of formatting the target node, we annotate the target node and format the
            // whole compilation unit and -using an annotation- find the formatted node in the
            // new syntax tree. That way we get the proper indentation for free.
            var annotation = new SyntaxAnnotation("formatOnTypeHelper");
            var newRoot = tree.GetRoot().ReplaceNode(target, target.WithAdditionalAnnotations(annotation));
            var formatted = Formatter.Format(newRoot, target.FullSpan, workspace, options);

            var node = formatted.GetAnnotatedNodes(annotation).FirstOrDefault();
            if (node == null)
            {
                return Enumerable.Empty<TextChange>();
            }

            var linePositionSpan = tree.GetText().Lines.GetLinePositionSpan(target.FullSpan);
            var newText = node.ToFullString();
            newText = EnsureProperNewLine(newText, options);

            return new TextChange[]
            {
                new TextChange(newText, linePositionSpan.Start, linePositionSpan.End)
            };
        }

        private static string EnsureProperNewLine(string text, OptionSet options)
        {
            // workaround: https://roslyn.codeplex.com/workitem/484
            var option = options.GetOption(Microsoft.CodeAnalysis.Formatting.FormattingOptions.NewLine, LanguageNames.CSharp);
            return text.Replace("\r\n", option);
        }

        public static SyntaxNode FindFormatTarget(SyntaxTree tree, int position)
        {
            // todo@jo - refine this
            var token = tree.GetRoot().FindToken(position);
            var kind = token.CSharpKind();

            switch (kind)
            {
                // ; -> use the statement
                case SyntaxKind.SemicolonToken:
                    return token.Parent;

                // } -> use the parent of the {}-block or
                // just the parent (XYZDeclaration etc)
                case SyntaxKind.CloseBraceToken:
                    var parent = token.Parent;
                    return parent.IsKind(SyntaxKind.Block)
                        ? parent.Parent
                        : parent;
            }

            return null;
        }
    }
}