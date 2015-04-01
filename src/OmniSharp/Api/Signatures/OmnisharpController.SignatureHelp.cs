using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {

        [HttpPost("signatureHelp")]
        public async Task<SignatureHelp> GetSignatureHelp(Request request)
        {
            foreach (var document in _workspace.GetDocuments(request.FileName))
            {
                var response = await GetSignatureHelp(document, request);
                if (response != null)
                {
                    return response;
                }
            }
            return null;
        }

        private async Task<SignatureHelp> GetSignatureHelp(Document document, Request request)
        {
            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));

            var invocation = await FindInvocationExpression(document, position);
            if (invocation == null)
            {
                return null;
            }

            var methodSymbol = await SymbolFinder.FindSymbolAtPositionAsync(document, invocation.Expression.Span.End) as IMethodSymbol;
            if (methodSymbol == null)
            {
                return null;
            }

            var activeSymbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position);

            return BuildSignatureHelp(methodSymbol, activeSymbol);
        }

        private SignatureHelp BuildSignatureHelp(IMethodSymbol methodSymbol, ISymbol parameterCandidate)
        {
            var signatures = new List<SignatureHelpItem>();
            foreach (var overload in methodSymbol.ContainingType.GetMembers(methodSymbol.Name))
            {
                var methodOverload = (IMethodSymbol)overload;
                var signatureItem = new SignatureHelpItem();
                signatureItem.Name = methodOverload.Name;
                signatureItem.Documentation = methodOverload.GetDocumentationCommentXml();
                signatureItem.Parameters = methodOverload.Parameters.Select(parameter =>
                {
                    return new SignatureHelpParameter()
                    {
                        Name = parameter.Name,
                        Type = parameter.ToDisplayString(),
                        Documentation = parameter.GetDocumentationCommentXml()
                    };
                });

                signatures.Add(signatureItem);
            }

            var signatureHelp = new SignatureHelp();
            signatureHelp.Signatures = signatures;

            return signatureHelp;
        }

        private async Task<InvocationExpressionSyntax> FindInvocationExpression(Document document, int position)
        {
            var tree = await document.GetSyntaxTreeAsync();
            var node = tree.GetRoot().FindToken(position).Parent;

            while (node != null)
            {
                var invocation = node as InvocationExpressionSyntax;
                if (invocation != null && invocation.ArgumentList.FullSpan.IntersectsWith(position))
                {
                    return invocation;
                }
                node = node.Parent;
            }

            return null;
        }
    }
}
