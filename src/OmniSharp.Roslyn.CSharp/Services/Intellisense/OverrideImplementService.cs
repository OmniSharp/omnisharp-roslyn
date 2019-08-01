using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.v1.OverrideImplement;
using OmniSharp.Roslyn.CSharp.Helpers;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace OmniSharp.Roslyn.CSharp.Services.OverrideImplement
{
    [OmniSharpHandler(OmniSharpEndpoints.OverrideImplement, LanguageNames.CSharp)]
    public class OverrideImplementService : IRequestHandler<OverrideImplementRequest, OverrideImplementResponce>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly ILogger<OverrideImplementService> _logger;

        [ImportingConstructor]
        public OverrideImplementService(OmniSharpWorkspace workspace, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _logger = loggerFactory.CreateLogger<OverrideImplementService>();
        }

        public async Task<OverrideImplementResponce> Handle(OverrideImplementRequest request)
        {
            var changes = new List<LinePositionSpanTextChange>();
            try
            {
                var document = _workspace.GetDocument(request.FileName);
                var semanticModel = await document.GetSemanticModelAsync();
                var sourceText = await document.GetTextAsync();

                var startPos = sourceText.Lines.GetPosition(new LinePosition(request.Line, 0));
                var currentPos = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));

                var oldRoot = semanticModel.SyntaxTree.GetRoot();
                var currentNode = oldRoot.FindNode(new TextSpan(currentPos, 0));

                var parentNode = currentNode.FirstAncestorOrSelf<ClassDeclarationSyntax>();
                var parentSymbol = semanticModel.GetDeclaredSymbol(parentNode);

                var overrideSymbol = OverrideHelpers.FindTargetSymbol(parentSymbol, request.OverrideTarget);
                if (overrideSymbol != null)
                {
                    var namespaces = new SortedSet<string>();
                    var replacement = OverrideHelpers.GetOverrideImplementString(overrideSymbol, namespaces);
                    var newSourceText = sourceText.Replace(startPos, currentPos - startPos, replacement);

                    newSourceText = AddMissingUsing(newSourceText, parentNode, namespaces);

                    var changeRanges = sourceText.GetChangeRanges(newSourceText);
                    var newRoot = CSharpSyntaxTree.ParseText(newSourceText).GetRoot();
                    newRoot = Formatter.Format(newRoot, changeRanges.Select(x => x.Span), _workspace);

                    foreach (var change in newRoot.SyntaxTree.GetChanges(oldRoot.SyntaxTree))
                    {
                        var lineSpan = oldRoot.SyntaxTree.GetLineSpan(change.Span);
                        changes.Add(new LinePositionSpanTextChange()
                        {
                            NewText = change.NewText,
                            StartLine = lineSpan.StartLinePosition.Line,
                            StartColumn = lineSpan.StartLinePosition.Character,
                            EndLine = lineSpan.EndLinePosition.Line,
                            EndColumn = lineSpan.EndLinePosition.Character,
                        });
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return new OverrideImplementResponce()
            {
                Changes = changes
            };
        }

        private SourceText AddMissingUsing(SourceText sourceText, ClassDeclarationSyntax classNode, IEnumerable<string> namespaces)
        {
            var parents = classNode.Ancestors().Where(x => x.Kind() == SyntaxKind.NamespaceDeclaration || x.Kind() == SyntaxKind.CompilationUnit);
            var usings = parents.Select(x => x.GetUsings());

            var currentNamespace = classNode.GetNamespace();
            var usingNames = usings.SelectMany(x => x.Select(y => y.Name.ToString())).ToHashSet();

            usingNames.Add(currentNamespace);

            var newLine = _workspace.Options.GetOption(FormattingOptions.NewLine, LanguageNames.CSharp);
            var changes = new List<TextChange>();
            foreach (var @namespace in namespaces)
            {
                if (!usingNames.Contains(@namespace) && !currentNamespace.StartsWith(@namespace + "."))
                {
                    var replacement = $"using {@namespace};{newLine}";
                    changes.Add(new TextChange(new TextSpan(GetUsingPosition(parents, @namespace), 0), replacement));
                }
            }
            return sourceText.WithChanges(changes);
        }

        private int GetUsingPosition(IEnumerable<SyntaxNode> nodes, string @namespace)
        {
            foreach (var node in nodes)
            {
                var usings = node.GetUsings();
                if (usings.Any())
                {
                    var @using = usings.Where(x => string.Compare(x.Name.ToString(), @namespace) > 0).FirstOrDefault();
                    return @using.FullSpan.Start;
                }
                var token = node.ChildTokens().Where(x => x.Kind() == SyntaxKind.OpenBraceToken).FirstOrDefault();
                if (token != null)
                {
                    return token.Span.End;
                }
            }
            return 0;
        }
    }
}
