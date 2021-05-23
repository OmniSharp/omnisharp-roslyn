#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.v1.InlineValues;
using System.Collections.Generic;
using OmniSharp.Roslyn.Extensions;
using System.Composition;
using System.Threading.Tasks;

namespace OmniSharp.Roslyn.CSharp.Services.InlineValues
{
    [OmniSharpHandler(OmniSharpEndpoints.InlineValues, LanguageNames.CSharp)]
    public class InlineValuesService : IRequestHandler<InlineValuesRequest, InlineValuesResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly ILogger _logger;
        private readonly InlineValuesCache _inlineValuesCache = new();

        [ImportingConstructor]
        public InlineValuesService(OmniSharpWorkspace workspace, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _logger = loggerFactory.CreateLogger<InlineValuesService>();
        }

        public async Task<InlineValuesResponse> Handle(InlineValuesRequest request)
        {
            _logger.LogTrace("Inline values requested");
            if (request.FileName.EndsWith("csx"))
            {
                _logger.LogInformation("Scripting is not supported for inline values.");
                return new InlineValuesResponse();
            }

            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                _logger.LogDebug("Could not find document {0}", request.FileName);
                return new InlineValuesResponse();
            }

            var sourceText = await document.GetTextAsync();
            var syntaxRoot = await document.GetSyntaxRootAsync();
            var semanticModel = await document.GetSemanticModelAsync();

            if (sourceText == null || syntaxRoot == null || semanticModel == null)
            {
                _logger.LogWarning("Document {0} had null information!", document.FilePath);
                return new InlineValuesResponse();
            }

            // We want to get the currently-active method. This _almost_ always has a surrounding
            // member declaration, but won't in the case of scripting or top-level statements

            var activeSpan = sourceText.GetSpanFromRange(request.Context.StoppedLocation);
            var activeNode = syntaxRoot.FindNode(activeSpan);
            SyntaxNode? containingMember = walkParentUntilFound(activeNode);
            if (containingMember is null)
            {
                // Couldn't find the context, just return early.
                _logger.LogWarning("Could not find context for {0}:{1}", document.FilePath, request.Context.StoppedLocation);
                return new InlineValuesResponse();
            }
            else if (containingMember is TypeDeclarationSyntax or EventDeclarationSyntax)
            {
                _logger.LogInformation("Request was for type or event {0}:{1}", document.FilePath, request.Context.StoppedLocation);
                return new InlineValuesResponse();

            }

            var operation = semanticModel.GetOperation(containingMember);
            if (operation == null)
            {
                _logger.LogWarning("Could not find operation node for {0}:{1}", document.FilePath, containingMember.Span);
                return new InlineValuesResponse();
            }

            // Try and retrieve existing values from the cache
            if (_inlineValuesCache.TryGetCachedValues(document.Id, operation.Syntax.Span) is { } cachedResult)
            {
                _logger.LogTrace("Retrieved values from the cache");
                return new InlineValuesResponse { Values = cachedResult };
            }

            // Nothing in the cache, so let's calculate the info.
            var values = CalculateValues(sourceText, operation);
            _inlineValuesCache.CacheResults(document.Id, operation.Syntax.Span, values);
            return new InlineValuesResponse { Values = values };

            static SyntaxNode? walkParentUntilFound(SyntaxNode node)
            {
                // Already walked up as far as we can go.
                if (node is MemberDeclarationSyntax)
                {
                    return adjustGlobalStatementIfNeeded(node);
                }

                SyntaxNode? current;
                for (current = node.Parent; current != null; current = current.Parent)
                {
                    switch (current)
                    {
                        case AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or MemberDeclarationSyntax:
                            return adjustGlobalStatementIfNeeded(current);
                    }
                }

                return null;

                static SyntaxNode adjustGlobalStatementIfNeeded(SyntaxNode node)
                    // IOperation for top-level statements returns the whole tree on the CompilationUnit
                    => node is GlobalStatementSyntax ? node.SyntaxTree.GetRoot() : node;
            }
        }

        private List<InlineValue> CalculateValues(SourceText sourceText, IOperation operation)
        {
            // There are 4 different types of values that we can return:
            //
            //   1. Local Variables
            //   2. Parameters
            //   3. Fields
            //   4. Properties
            //
            // The last 2 have some special conditions attached to them: we only want to have the debugger
            // retrieve values that are either instance values on `this`, or static values on the current
            // type. For properties access off of other things, we have no way to guarantee that the thing
            // accessing the property exists: for example it could be a local variable that is currently
            // null. Causing the debugger to trigger a number of null reference exceptions is probably not
            // what the user is looking for.
            // Additionally, fields and properties can be shadowed by locals and parameters. This
            // presents a particular problem for static members in generic contexts: we have no way of
            // knowing the type arguments of the current context, so for these cases we have to skip.
            //
            // We need to use EvaluatableExpression for all contexts here, because the debugger renames variables
            // to have their types in the name. We don't know the exact name the debugger used here, so we
            // just have to have the debugger evaluate the expression to get the actual value.

            var results = new List<InlineValue>();
            IEnumerable<IOperation> descendants = operation.Descendants(descendIntoChildren: static node => node is not (ILocalFunctionOperation or IAnonymousFunctionOperation));
            foreach (var node in descendants)
            {
                switch (node)
                {
                    case ILocalReferenceOperation { Local: var local }:
                        results.Add(new InlineValue
                        {
                            Kind = InlineValueKind.EvaluatableExpression,
                            Text = local.Name,
                            Range = sourceText.GetRangeFromSpan(node.Syntax.Span)
                        });
                        break;

                    case IParameterReferenceOperation { Parameter: var parameter }:
                        results.Add(new InlineValue
                        {
                            Kind = InlineValueKind.EvaluatableExpression,
                            Text = parameter.Name,
                            Range = sourceText.GetRangeFromSpan(node.Syntax.Span)
                        });
                        break;

                    case IVariableDeclaratorOperation { Symbol: ILocalSymbol local }:
                        var span = (node.Syntax as VariableDeclaratorSyntax)?.Identifier.Span ?? node.Syntax.Span;
                        results.Add(new InlineValue
                        {
                            Kind = InlineValueKind.EvaluatableExpression,
                            Text = local.Name,
                            Range = sourceText.GetRangeFromSpan(span)
                        });
                        break;
                        

                    case IPropertyReferenceOperation
                    {
                        Property: var property,
                        Instance: null or IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }
                    } when isValidReference(property):
                        results.Add(new InlineValue
                        {
                            Kind = InlineValueKind.EvaluatableExpression,
                            Text = getText(property),
                            Range = sourceText.GetRangeFromSpan(node.Syntax.Span)
                        });
                        break;

                    case IFieldReferenceOperation
                    {
                        Field: var field,
                        Instance: null or IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }
                    } when isValidReference(field):
                        results.Add(new InlineValue
                        {
                            Kind = InlineValueKind.EvaluatableExpression,
                            Text = getText(field),
                            Range = sourceText.GetRangeFromSpan(node.Syntax.Span)
                        });
                        break;
                }
            }

            return results;

            static bool isValidReference(ISymbol reference)
            {
                if (!reference.IsStatic)
                {
                    return true;
                }

                var containingType = reference.ContainingType;
                while (containingType != null)
                {
                    if (containingType.IsGenericType)
                    {
                        return false;
                    }

                    containingType = containingType.ContainingType;
                }

                return true;
            }

            static string getText(ISymbol symbol)
                => symbol.IsStatic ? $"{symbol.ContainingType.ToNameDisplayString()}.{symbol.ToNameDisplayString()}" : $"this.{symbol.Name}";
        }
    }
}
