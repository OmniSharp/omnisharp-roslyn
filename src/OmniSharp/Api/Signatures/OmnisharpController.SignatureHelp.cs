using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
            var tree = await document.GetSyntaxTreeAsync();
            var node = tree.GetRoot().FindToken(position).Parent;

            while (node != null)
            {
                var invocation = node as InvocationExpressionSyntax;
                if (invocation != null && invocation.ArgumentList.FullSpan.IntersectsWith(position))
                {
                    return await GetSignatureHelp(document, invocation.Expression, invocation.ArgumentList, position);
                }

                var objectCreation = node as ObjectCreationExpressionSyntax;
                if (objectCreation != null && objectCreation.ArgumentList.FullSpan.IntersectsWith(position))
                {
                    return await GetSignatureHelp(document, objectCreation, objectCreation.ArgumentList, position);
                }

                node = node.Parent;
            }

            return null;
        }

        private async Task<SignatureHelp> GetSignatureHelp(Document document, SyntaxNode expression, ArgumentListSyntax argumentList, int position)
        {
            var semanticModel = await document.GetSemanticModelAsync();
            var signatureHelp = new SignatureHelp();

            // define active parameter by position
            foreach (var comma in argumentList.Arguments.GetSeparators())
            {
                if (comma.Span.Start > position)
                {
                    break;
                }
                signatureHelp.ActiveParameter += 1;
            }

            // collect types of invocation to select overload
            var typeInfos = argumentList.Arguments
                .Select(argument => semanticModel.GetTypeInfo(argument.Expression));

            // process overloads
            var bestScore = int.MinValue;
            var signatures = new List<SignatureHelpItem>();
            signatureHelp.Signatures = signatures;
            foreach (var methodSymbol in GetMethodOverloads(semanticModel, expression))
            {
                var thisScore = InvocationScore(methodSymbol, typeInfos);
                if (thisScore > bestScore)
                {
                    bestScore = thisScore;
                    signatureHelp.ActiveSignature = signatures.Count;
                }

                signatures.Add(BuildSignature(methodSymbol));
            }

            return signatureHelp;
        }

        private IEnumerable<IMethodSymbol> GetMethodOverloads(SemanticModel semanticModel, SyntaxNode node)
        {
            ISymbol symbol = null;
            var symbolInfo = semanticModel.GetSymbolInfo(node);
            if (symbolInfo.Symbol != null)
            {
                symbol = symbolInfo.Symbol;
            }
            else if (!symbolInfo.CandidateSymbols.IsEmpty)
            {
                symbol = symbolInfo.CandidateSymbols.First();
            }

            if (symbol == null || symbol.ContainingType == null)
            {
                return new IMethodSymbol[] { };
            }

            return symbol.ContainingType.GetMembers(symbol.Name).OfType<IMethodSymbol>();
        }

        private int InvocationScore(IMethodSymbol symbol, IEnumerable<TypeInfo> types)
        {
            var parameters = GetParameters(symbol);
            if (parameters.Count() < types.Count())
            {
                return int.MinValue;
            }

            var score = 0;
            var invocationEnum = types.GetEnumerator();
            var definitionEnum = parameters.GetEnumerator();
            while (invocationEnum.MoveNext() && definitionEnum.MoveNext())
            {
                if (invocationEnum.Current.ConvertedType == null)
                {
                    // 1 point for having a parameter
                    score += 1;
                }
                else if (invocationEnum.Current.ConvertedType.Equals(definitionEnum.Current.Type))
                {
                    // 2 points for having a parameter and being
                    // the same type
                    score += 2;
                }
            }

            return score;
        }

        private SignatureHelpItem BuildSignature(IMethodSymbol symbol)
        {
            var signature = new SignatureHelpItem();
            signature.Documentation = symbol.GetDocumentationCommentXml();
            if (symbol.MethodKind == MethodKind.Constructor)
            {
                signature.Name = symbol.ContainingType.Name;
                signature.Label = symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            }
            else
            {
                signature.Name = symbol.Name;
                signature.Label = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            }

            signature.Parameters = GetParameters(symbol).Select(parameter =>
            {
                return new SignatureHelpParameter()
                {
                    Name = parameter.Name,
                    Label = parameter.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    Documentation = parameter.GetDocumentationCommentXml()
                };
            });
            return signature;
        }

        private static IEnumerable<IParameterSymbol> GetParameters(IMethodSymbol methodSymbol)
        {
            if (!methodSymbol.IsExtensionMethod)
            {
                return methodSymbol.Parameters;
            }
            else
            {
                return methodSymbol.Parameters.RemoveAt(0);
            }
        }
    }
}
