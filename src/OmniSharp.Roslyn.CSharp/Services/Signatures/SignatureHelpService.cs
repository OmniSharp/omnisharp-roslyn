using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
            foreach (var comma in invocations.First().Separators)
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
                var types = invocation.ArgumentTypes;
                ISymbol throughSymbol = null;
                ISymbol throughType = null;
                var methodGroup = invocation.SemanticModel.GetMemberGroup(invocation.Receiver).OfType<IMethodSymbol>();
                if (invocation.Receiver is MemberAccessExpressionSyntax)
                {
                    var throughExpression = ((MemberAccessExpressionSyntax)invocation.Receiver).Expression;
                    throughSymbol = invocation.SemanticModel.GetSpeculativeSymbolInfo(invocation.Position, throughExpression, SpeculativeBindingOption.BindAsExpression).Symbol;
                    throughType = invocation.SemanticModel.GetSpeculativeTypeInfo(invocation.Position, throughExpression, SpeculativeBindingOption.BindAsTypeOrNamespace).Type;
                    var includeInstance = throughSymbol != null && !(throughSymbol is ITypeSymbol);
                    var includeStatic = (throughSymbol is INamedTypeSymbol) || throughType != null;
                    methodGroup = methodGroup.Where(m => (m.IsStatic && includeStatic) || (!m.IsStatic && includeInstance));
                }
                else if (invocation.Receiver is SimpleNameSyntax && invocation.IsInStaticContext)
                {
                    methodGroup = methodGroup.Where(m => m.IsStatic);
                }

                foreach (var methodOverload in methodGroup)
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
                if (node is InvocationExpressionSyntax invocation && invocation.ArgumentList.Span.Contains(position))
                {
                    var semanticModel = await document.GetSemanticModelAsync();
                    return new InvocationContext(semanticModel, position, invocation.Expression, invocation.ArgumentList, invocation.IsInStaticContext());
                }

                if (node is ObjectCreationExpressionSyntax objectCreation && objectCreation.ArgumentList.Span.Contains(position))
                {
                    var semanticModel = await document.GetSemanticModelAsync();
                    return new InvocationContext(semanticModel, position, objectCreation, objectCreation.ArgumentList, objectCreation.IsInStaticContext());
                }

                if (node is AttributeSyntax attributeSyntax && attributeSyntax.ArgumentList.Span.Contains(position))
                {
                    var semanticModel = await document.GetSemanticModelAsync();
                    return new InvocationContext(semanticModel, position, attributeSyntax, attributeSyntax.ArgumentList, attributeSyntax.IsInStaticContext());
                }

                node = node.Parent;
            }

            return null;
        }

        private int InvocationScore(IMethodSymbol symbol, IEnumerable<TypeInfo> types)
        {
            var parameters = symbol.Parameters;
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

            signature.Parameters = symbol.Parameters.Select(parameter =>
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

    }
}
