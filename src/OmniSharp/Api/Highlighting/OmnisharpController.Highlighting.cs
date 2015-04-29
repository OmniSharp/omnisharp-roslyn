using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Documentation;
using OmniSharp.Extensions;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("highlight")]
        public async Task<IEnumerable<HighlightResponse>> Highlight(HighlightRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            var model = await document.GetSemanticModelAsync();
            var tree = await document.GetSyntaxTreeAsync();
            var root = await tree.GetRootAsync();

            var nodes = new List<SyntaxNode>();
            if (request.Lines.Length == 0)
            {
                nodes.Add(root);
            }
            else
            {
                var text = await tree.GetTextAsync();
                foreach (var line in request.Lines)
                {
                    var lineSpan = text.Lines[line].Span;
                    var node = root;
                    var start = node.ChildThatContainsPosition(lineSpan.Start);
                    var end = node.ChildThatContainsPosition(lineSpan.End);
                    while (start.IsNode && start == end)
                    {
                        node = start.AsNode();
                    }

                    nodes.Add(node);
                }
            }

            var walker = new HighlightSyntaxWalker(model);
            foreach (var node in nodes)
            {
                walker.Visit(node);
            }

            return walker.Regions;
        }

        class HighlightSyntaxWalker : CSharpSyntaxWalker
        {
            readonly SemanticModel _model;
            readonly List<HighlightResponse> _regions;

            // TODO: Fix overlap
            public IImmutableList<HighlightResponse> Regions
            {
                get
                {
                    return _regions
                        .GroupBy(r => new { r.Line, r.Start, r.End })
                        .Select(g => new HighlightResponse
                        {
                            Line = g.Key.Line,
                            Start = g.Key.Start,
                            End = g.Key.End,
                            Kind = string.Join(" ", g.Select(r => r.Kind).Distinct())
                        })
                        .ToImmutableList();
                }
            }

            public HighlightSyntaxWalker(SemanticModel model)
                : base(SyntaxWalkerDepth.Trivia)
            {
                _model = model;
            }

            void Mark(SyntaxToken token, string type = null)
            {
                var location = token.GetLocation().GetLineSpan();
                var startLine = location.StartLinePosition.Line;
                var endLine = location.EndLinePosition.Line;

                for (var i = startLine; i <= endLine; i++)
                {
                    var start = i == startLine ? location.StartLinePosition.Character : 0;
                    var end = i == endLine ? location.EndLinePosition.Character : int.MaxValue;

                    _regions.Add(new HighlightResponse
                    {
                        Line = i,
                        Start = start,
                        End = end,
                        Kind = type
                    });
                }
            }

            void Mark(SyntaxTrivia trivia, string type = null)
            {
                var location = trivia.GetLocation().GetLineSpan();
                var startLine = location.StartLinePosition.Line;
                var endLine = location.EndLinePosition.Line;

                for (var i = startLine; i <= endLine; i++)
                {
                    var start = i == startLine ? location.StartLinePosition.Character : 0;
                    var end = i == endLine ? location.EndLinePosition.Character : int.MaxValue;

                    _regions.Add(new HighlightResponse
                    {
                        Line = i,
                        Start = start,
                        End = end,
                        Kind = type
                    });
                }
            }

            ISymbol GetTokenSymbol(SyntaxToken token)
            {
                var symbol = _model.GetDeclaredSymbol(token.Parent);
                if (symbol == null)
                {
                    // The token isnt part of a declaration node, so try to get symbol info.
                    symbol = _model.GetSymbolInfo(token.Parent).Symbol;
                    if (symbol == null)
                    {
                        // we couldnt find symbol information for the node, so we will look at all symbols in scope by name.
                        var namedSymbols = _model.LookupSymbols(token.SpanStart, null, token.ToString(), true);
                        if (namedSymbols.Length == 1)
                        {
                            symbol = namedSymbols[0];
                        }
                    }
                }

                return symbol;
            }

            void MarkIdentifier(SyntaxToken token)
            {
                var symbol = GetTokenSymbol(token);
                if (symbol == null)
                {
                    Mark(token, "identifier");
                }
                else
                {
                    VisitSymbol(token, symbol);
                }
            }

            void VisitSymbol(SyntaxToken token, ISymbol symbol)
            {
                var parts = symbol.ToDisplayParts(new SymbolDisplayFormat());
                var part = parts.SingleOrDefault(p => p.Symbol == symbol);
                if (part.Symbol != null)
                {
                    Mark(token, part.Kind.ToString().ToLowerInvariant());
                }
            }

            public override void VisitToken(SyntaxToken token)
            {
                if (token.IsKeyword() || token.IsContextualKeyword())
                {
                    Mark(token, "keyword");
                    var symbol = GetTokenSymbol(token);
                    if (symbol != null)
                    {
                        VisitSymbol(token, symbol);
                    }
                }

                var kind = token.Kind();
                switch (kind)
                {
                    case SyntaxKind.IdentifierToken:
                        MarkIdentifier(token); break;

                    case SyntaxKind.StringLiteralToken:
                        Mark(token, "string"); break;

                    case SyntaxKind.NumericLiteralToken:
                        Mark(token, "number"); break;

                    case SyntaxKind.CharacterLiteralToken:
                        Mark(token, "char"); break;
                }

                base.VisitToken(token);
            }

            public override void VisitTrivia(SyntaxTrivia trivia)
            {
                if (trivia.HasStructure)
                    Visit(trivia.GetStructure());

                switch (trivia.Kind())
                {
                    case SyntaxKind.SingleLineDocumentationCommentTrivia:
                    case SyntaxKind.MultiLineDocumentationCommentTrivia:
                    case SyntaxKind.DocumentationCommentExteriorTrivia:
                        Mark(trivia, "doc");

                        goto case SyntaxKind.SingleLineCommentTrivia;

                    case SyntaxKind.MultiLineCommentTrivia:
                    case SyntaxKind.SingleLineCommentTrivia:
                        Mark(trivia, "comment"); break;

                    case SyntaxKind.EndOfLineTrivia:
                    case SyntaxKind.WhitespaceTrivia:
                        Mark(trivia, "whitespace"); break;

                    case SyntaxKind.RegionDirectiveTrivia:
                    case SyntaxKind.EndRegionDirectiveTrivia:
                        Mark(trivia, "region"); break;

                    case SyntaxKind.DisabledTextTrivia:
                        Mark(trivia, "disabled-text"); break;
                }

                base.VisitTrivia(trivia);
            }
        }
    }
}
