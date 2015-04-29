using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Text;
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
            var highlightFile = request.Lines == null || request.Lines.Length == 0;
            if (highlightFile)
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
                    var next = node;
                    SyntaxNodeOrToken start, end;
                    do
                    {
                        node = next;
                        start = node.ChildThatContainsPosition(lineSpan.Start);
                        end = node.ChildThatContainsPosition(lineSpan.End);
                        next = start.AsNode();
                    } while (start.IsNode && start == end);

                    nodes.Add(node);
                }
            }

            var walker = new HighlightSyntaxWalker(model);
            foreach (var node in nodes)
            {
                walker.Visit(node);
            }

            var regions = walker.Regions;
            if (!highlightFile)
            {
                return regions.Where(r => request.Lines.Contains(r.Line));
            }

            return regions;
        }
    }
}
