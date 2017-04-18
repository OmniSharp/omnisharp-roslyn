using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.SignatureHelp;

namespace OmniSharp.Roslyn.CSharp.Services.Signatures
{
    [OmniSharpHandler(OmniSharpEndpoints.SignatureHelp, LanguageNames.CSharp)]
    public class SignatureHelpService : IRequestHandler<SignatureHelpRequest, SignatureHelpResponse>
    {
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public SignatureHelpService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<SignatureHelpResponse> Handle(SignatureHelpRequest request)
        {
            var invocations = new List<InvocationContext>();
            foreach (var document in _workspace.GetDocuments(request.FileName))
            {
                var invocation = await GetInvocation(document, request);
                if (invocation != null)
                {
                    invocations.Add(invocation);
                }
            }

            if (invocations.Count == 0)
            {
                return null;
            }

            var response = new SignatureHelpResponse();

            // define active parameter by position
            foreach (var comma in invocations.First().ArgumentList.Arguments.GetSeparators())
            {
                if (comma.Span.Start > invocations.First().Position)
                {
                    break;
                }
                response.ActiveParameter += 1;
            }

            // process all signatures, define active signature by types
            var signaturesSet = new HashSet<SignatureHelpItem>();
            var bestScore = int.MinValue;
            SignatureHelpItem bestScoredItem = null;

            foreach (var invocation in invocations)
            {
                var types = invocation.ArgumentList.Arguments
                    .Select(argument => invocation.SemanticModel.GetTypeInfo(argument.Expression));

                foreach (var methodOverload in GetMethodOverloads(invocation.SemanticModel, invocation.Receiver))
                {
                    var signature = BuildSignature(methodOverload);
                    signaturesSet.Add(signature);

                    var score = InvocationScore(methodOverload, types);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestScoredItem = signature;
                    }
                }
            }

            var signaturesList = signaturesSet.ToList();
            response.Signatures = signaturesList;
            response.ActiveSignature = signaturesList.IndexOf(bestScoredItem);

            return response;
        }

        private async Task<InvocationContext> GetInvocation(Document document, Request request)
        {
            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));
            var tree = await document.GetSyntaxTreeAsync();
            var root = await tree.GetRootAsync();
            var node = root.FindToken(position).Parent;

            // Walk up until we find a node that we're interested in.
            while (node != null)
            {
                var invocation = node as InvocationExpressionSyntax;
                if (invocation != null && invocation.ArgumentList.Span.Contains(position))
                {
                    return new InvocationContext()
                    {
                        SemanticModel = await document.GetSemanticModelAsync(),
                        Position = position,
                        Receiver = invocation.Expression,
                        ArgumentList = invocation.ArgumentList
                    };
                }

                var objectCreation = node as ObjectCreationExpressionSyntax;
                if (objectCreation != null && objectCreation.ArgumentList.Span.Contains(position))
                {
                    return new InvocationContext()
                    {
                        SemanticModel = await document.GetSemanticModelAsync(),
                        Position = position,
                        Receiver = objectCreation,
                        ArgumentList = objectCreation.ArgumentList
                    };
                }

                node = node.Parent;
            }

            return null;
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

        private static SignatureHelpItem BuildSignature(IMethodSymbol symbol)
        {
            var signature = new SignatureHelpItem();
            signature.Documentation = symbol.GetDocumentationCommentXml();
            signature.Name = symbol.MethodKind == MethodKind.Constructor ? symbol.ContainingType.Name : symbol.Name;
            signature.Label = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

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
