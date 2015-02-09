using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace OmniSharp
{
    // a little dance so that we can access the ```GetFormattedTextChanges``` method that is hidden by default.
    // see: https://github.com/dotnet/roslyn/blob/b6484300dfafb43af0c27e542ec457a7583e1aa8/src/Workspaces/Core/Portable/Formatting/Formatter.cs#L309
    static class FormatterReflect
    {
        private static readonly Type formatterType;
        private static readonly MethodInfo formatMethod;

        static FormatterReflect()
        {
            formatterType = typeof(Formatter);
            formatMethod = formatterType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Where(method => method.Name == "GetFormattedTextChanges" && method.GetParameters().Count() == 6)
                .FirstOrDefault();
        }

        public static IList<Microsoft.CodeAnalysis.Text.TextChange> GetFormattedTextChanges(SyntaxNode node, TextSpan span, Workspace workspace, OptionSet options = null)
        {
            return (IList<Microsoft.CodeAnalysis.Text.TextChange>)formatMethod.Invoke(null, new object[] { node, span, workspace, options, null, null });
        }
    }

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

            var changes = FormatterReflect.GetFormattedTextChanges(tree.GetRoot(), target.FullSpan, workspace, options);
            var lines = tree.GetText().Lines;
            var result = changes.Select(change =>
            {
                var linePositionSpan = lines.GetLinePositionSpan(change.Span);
                var newText = EnsureProperNewLine(change.NewText, options);

                return new TextChange()
                {
                    NewText = newText,
                    StartLine = linePositionSpan.Start.Line + 1,
                    StartColumn = linePositionSpan.Start.Character + 1,
                    EndLine = linePositionSpan.End.Line + 1,
                    EndColumn = linePositionSpan.End.Character + 1
                };
            });
            return result;
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