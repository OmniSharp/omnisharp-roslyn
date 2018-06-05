using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Mef;
using OmniSharp.Models.V2;
using OmniSharp.Models.V2.CodeStructure;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Structure
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.CodeStructure, LanguageNames.CSharp)]
    public class CodeStructureService : IRequestHandler<CodeStructureRequest, CodeStructureResponse>
    {
        private static readonly SymbolDisplayFormat s_ShortTypeFormat = new SymbolDisplayFormat(
            typeQualificationStyle:
                SymbolDisplayTypeQualificationStyle.NameOnly,
            genericsOptions:
                SymbolDisplayGenericsOptions.IncludeTypeParameters);

        private static readonly SymbolDisplayFormat s_TypeFormat = new SymbolDisplayFormat(
            typeQualificationStyle:
                SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions:
                SymbolDisplayGenericsOptions.IncludeTypeParameters |
                SymbolDisplayGenericsOptions.IncludeVariance);

        private static readonly SymbolDisplayFormat s_ShortMemberFormat = new SymbolDisplayFormat(
            genericsOptions:
                SymbolDisplayGenericsOptions.IncludeTypeParameters);

        private static readonly SymbolDisplayFormat s_MemberFormat = new SymbolDisplayFormat(
            genericsOptions:
                SymbolDisplayGenericsOptions.IncludeTypeParameters |
                SymbolDisplayGenericsOptions.IncludeVariance,
            memberOptions:
                SymbolDisplayMemberOptions.IncludeParameters,
            parameterOptions:
                SymbolDisplayParameterOptions.IncludeDefaultValue |
                SymbolDisplayParameterOptions.IncludeExtensionThis |
                SymbolDisplayParameterOptions.IncludeName |
                SymbolDisplayParameterOptions.IncludeParamsRefOut |
                SymbolDisplayParameterOptions.IncludeType,
            miscellaneousOptions:
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        private readonly OmniSharpWorkspace _workspace;
        private readonly IEnumerable<ICodeElementPropertyProvider> _propertyProviders;

        [ImportingConstructor]
        public CodeStructureService(
            OmniSharpWorkspace workspace,
            [ImportMany] IEnumerable<ICodeElementPropertyProvider> propertyProviders)
        {
            _workspace = workspace;
            _propertyProviders = propertyProviders;
        }

        public async Task<CodeStructureResponse> Handle(CodeStructureRequest request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return null;
            }

            var elements = await GetCodeElementsAsync(document);

            var response = new CodeStructureResponse
            {
                Elements = elements
            };

            return response;
        }

        private async Task<IReadOnlyList<CodeElement>> GetCodeElementsAsync(Document document)
        {
            var text = await document.GetTextAsync();
            var syntaxRoot = await document.GetSyntaxRootAsync();
            var semanticModel = await document.GetSemanticModelAsync();

            var results = ImmutableList.CreateBuilder<CodeElement>();

            foreach (var node in ((CompilationUnitSyntax)syntaxRoot).Members)
            {
                foreach (var element in CreateCodeElements(node, text, semanticModel))
                {
                    if (element != null)
                    {
                        results.Add(element);
                    }
                }
            }

            return results.ToImmutable();
        }

        private IEnumerable<CodeElement> CreateCodeElements(SyntaxNode node, SourceText text, SemanticModel semanticModel)
        {
            switch (node)
            {
                case TypeDeclarationSyntax typeDeclaration:
                    yield return CreateCodeElement(typeDeclaration, text, semanticModel);
                    break;
                case DelegateDeclarationSyntax delegateDeclaration:
                    yield return CreateCodeElement(delegateDeclaration, text, semanticModel);
                    break;
                case EnumDeclarationSyntax enumDeclaration:
                    yield return CreateCodeElement(enumDeclaration, text, semanticModel);
                    break;
                case NamespaceDeclarationSyntax namespaceDeclaration:
                    yield return CreateCodeElement(namespaceDeclaration, text, semanticModel);
                    break;
                case BaseMethodDeclarationSyntax baseMethodDeclaration:
                    yield return CreateCodeElement(baseMethodDeclaration, text, semanticModel);
                    break;
                case BasePropertyDeclarationSyntax basePropertyDeclaration:
                    yield return CreateCodeElement(basePropertyDeclaration, text, semanticModel);
                    break;
                case BaseFieldDeclarationSyntax baseFieldDeclaration:
                    foreach (var variableDeclarator in baseFieldDeclaration.Declaration.Variables)
                    {
                        yield return CreateCodeElement(variableDeclarator, baseFieldDeclaration, text, semanticModel);
                    }

                    break;
            }
        }

        private CodeElement CreateCodeElement(TypeDeclarationSyntax typeDeclaration, SourceText text, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
            if (symbol == null)
            {
                return null;
            }

            var builder = new CodeElement.Builder
            {
                Kind = GetKind(typeDeclaration, symbol),
                Name = symbol.ToDisplayString(s_ShortTypeFormat),
                DisplayName = symbol.ToDisplayString(s_TypeFormat)
            };

            AddRanges(builder, typeDeclaration.AttributeLists.Span, typeDeclaration.Span, typeDeclaration.Identifier.Span, text);
            AddSymbolProperties(symbol, builder);

            foreach (var member in typeDeclaration.Members)
            {
                foreach (var childElement in CreateCodeElements(member, text, semanticModel))
                {
                    builder.AddChild(childElement);
                }
            }

            return builder.ToCodeElement();
        }

        private CodeElement CreateCodeElement(DelegateDeclarationSyntax delegateDeclaration, SourceText text, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(delegateDeclaration);
            if (symbol == null)
            {
                return null;
            }

            var builder = new CodeElement.Builder
            {
                Kind = GetKind(delegateDeclaration, symbol),
                Name = symbol.ToDisplayString(s_ShortTypeFormat),
                DisplayName = symbol.ToDisplayString(s_TypeFormat),
            };

            AddRanges(builder, delegateDeclaration.AttributeLists.Span, delegateDeclaration.Span, delegateDeclaration.Identifier.Span, text);
            AddSymbolProperties(symbol, builder);

            return builder.ToCodeElement();
        }

        private CodeElement CreateCodeElement(EnumDeclarationSyntax enumDeclaration, SourceText text, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(enumDeclaration);
            if (symbol == null)
            {
                return null;
            }

            var builder = new CodeElement.Builder
            {
                Kind = GetKind(enumDeclaration, symbol),
                Name = symbol.ToDisplayString(s_ShortTypeFormat),
                DisplayName = symbol.ToDisplayString(s_TypeFormat),
            };

            AddRanges(builder, enumDeclaration.AttributeLists.Span, enumDeclaration.Span, enumDeclaration.Identifier.Span, text);
            AddSymbolProperties(symbol, builder);

            foreach (var member in enumDeclaration.Members)
            {
                foreach (var childElement in CreateCodeElements(member, text, semanticModel))
                {
                    builder.AddChild(childElement);
                }
            }

            return builder.ToCodeElement();
        }

        private CodeElement CreateCodeElement(NamespaceDeclarationSyntax namespaceDeclaration, SourceText text, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(namespaceDeclaration);
            if (symbol == null)
            {
                return null;
            }

            var builder = new CodeElement.Builder
            {
                Kind = GetKind(namespaceDeclaration, symbol),
                Name = symbol.ToDisplayString(s_ShortTypeFormat),
                DisplayName = symbol.ToDisplayString(s_TypeFormat),
            };

            AddRanges(builder, attributesSpan: default, namespaceDeclaration.Span, namespaceDeclaration.Name.Span, text);

            foreach (var member in namespaceDeclaration.Members)
            {
                foreach (var childElement in CreateCodeElements(member, text, semanticModel))
                {
                    builder.AddChild(childElement);
                }
            }

            return builder.ToCodeElement();
        }

        private CodeElement CreateCodeElement(BaseMethodDeclarationSyntax baseMethodDeclaration, SourceText text, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(baseMethodDeclaration);
            if (symbol == null)
            {
                return null;
            }

            var builder = new CodeElement.Builder
            {
                Kind = GetKind(baseMethodDeclaration, symbol),
                Name = symbol.ToDisplayString(s_ShortMemberFormat),
                DisplayName = symbol.ToDisplayString(s_MemberFormat),
            };

            AddRanges(builder, baseMethodDeclaration.AttributeLists.Span, baseMethodDeclaration.Span, GetNameSpan(baseMethodDeclaration), text);
            AddSymbolProperties(symbol, builder);

            return builder.ToCodeElement();
        }

        private CodeElement CreateCodeElement(BasePropertyDeclarationSyntax basePropertyDeclaration, SourceText text, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(basePropertyDeclaration);
            if (symbol == null)
            {
                return null;
            }

            var builder = new CodeElement.Builder
            {
                Kind = GetKind(basePropertyDeclaration, symbol),
                Name = symbol.ToDisplayString(s_ShortMemberFormat),
                DisplayName = symbol.ToDisplayString(s_MemberFormat),
            };

            AddRanges(builder, basePropertyDeclaration.AttributeLists.Span, basePropertyDeclaration.Span, GetNameSpan(basePropertyDeclaration), text);
            AddSymbolProperties(symbol, builder);

            return builder.ToCodeElement();
        }

        private CodeElement CreateCodeElement(VariableDeclaratorSyntax variableDeclarator, BaseFieldDeclarationSyntax baseFieldDeclaration, SourceText text, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(variableDeclarator);
            if (symbol == null)
            {
                return null;
            }

            var builder = new CodeElement.Builder
            {
                Kind = GetKind(baseFieldDeclaration, symbol),
                Name = symbol.ToDisplayString(s_ShortMemberFormat),
                DisplayName = symbol.ToDisplayString(s_MemberFormat),
            };

            AddRanges(builder, baseFieldDeclaration.AttributeLists.Span, variableDeclarator.Span, variableDeclarator.Identifier.Span, text);
            AddSymbolProperties(symbol, builder);

            return builder.ToCodeElement();
        }

        private static string GetKind(SyntaxNode node, ISymbol symbol)
        {
            switch (node.Kind())
            {
                case SyntaxKind.ClassDeclaration:
                    return CodeElementKinds.Class;
                case SyntaxKind.ConstructorDeclaration:
                    return CodeElementKinds.Constructor;
                case SyntaxKind.DelegateDeclaration:
                    return CodeElementKinds.Delegate;
                case SyntaxKind.DestructorDeclaration:
                    return CodeElementKinds.Destructor;
                case SyntaxKind.EnumDeclaration:
                    return CodeElementKinds.Enum;
                case SyntaxKind.EnumMemberDeclaration:
                    return CodeElementKinds.EnumMember;
                case SyntaxKind.EventDeclaration:
                case SyntaxKind.EventFieldDeclaration:
                    return CodeElementKinds.Event;
                case SyntaxKind.FieldDeclaration:
                    return ((IFieldSymbol)symbol).IsConst
                        ? CodeElementKinds.Constant
                        : CodeElementKinds.Field;
                case SyntaxKind.IndexerDeclaration:
                    return CodeElementKinds.Indexer;
                case SyntaxKind.InterfaceDeclaration:
                    return CodeElementKinds.Interface;
                case SyntaxKind.MethodDeclaration:
                    return CodeElementKinds.Method;
                case SyntaxKind.NamespaceDeclaration:
                    return CodeElementKinds.Namespace;
                case SyntaxKind.OperatorDeclaration:
                case SyntaxKind.ConversionOperatorDeclaration:
                    return CodeElementKinds.Operator;
                case SyntaxKind.PropertyDeclaration:
                    return CodeElementKinds.Property;
                case SyntaxKind.StructDeclaration:
                    return CodeElementKinds.Struct;
                default:
                    return CodeElementKinds.Unknown;
            }
        }

        private static string GetAccessibility(ISymbol symbol)
        {
            switch (symbol.DeclaredAccessibility)
            {
                case Accessibility.Public:
                    return CodeElementAccessibilities.Public;
                case Accessibility.Internal:
                    return CodeElementAccessibilities.Internal;
                case Accessibility.Private:
                    return CodeElementAccessibilities.Private;
                case Accessibility.Protected:
                    return CodeElementAccessibilities.Protected;
                case Accessibility.ProtectedOrInternal:
                    return CodeElementAccessibilities.ProtectedInternal;
                case Accessibility.ProtectedAndInternal:
                    return CodeElementAccessibilities.PrivateProtected;
                default:
                    return null;
            }
        }

        private static TextSpan GetNameSpan(BaseMethodDeclarationSyntax baseMethodDeclaration)
        {
            switch (baseMethodDeclaration)
            {
                case MethodDeclarationSyntax methodDeclaration:
                    return methodDeclaration.Identifier.Span;
                case ConstructorDeclarationSyntax constructorDeclaration:
                    return constructorDeclaration.Identifier.Span;
                case DestructorDeclarationSyntax destructorDeclaration:
                    return destructorDeclaration.Identifier.Span;
                case OperatorDeclarationSyntax operatorDeclaration:
                    return operatorDeclaration.OperatorToken.Span;
                case ConversionOperatorDeclarationSyntax conversionOperatorDeclaration:
                    return conversionOperatorDeclaration.Type.Span;
                default:
                    return default;
            }
        }

        private static TextSpan GetNameSpan(BasePropertyDeclarationSyntax basePropertyDeclaration)
        {
            switch (basePropertyDeclaration)
            {
                case PropertyDeclarationSyntax propertyDeclaration:
                    return propertyDeclaration.Identifier.Span;
                case EventDeclarationSyntax eventDeclaration:
                    return eventDeclaration.Identifier.Span;
                case IndexerDeclarationSyntax indexerDeclaration:
                    return indexerDeclaration.ThisKeyword.Span;
                default:
                    return default;
            }
        }

        private static Range CreateRange(TextSpan span, SourceText text)
        {
            var startLine = text.Lines.GetLineFromPosition(span.Start);
            var endLine = text.Lines.GetLineFromPosition(span.End);

            return new Range
            {
                Start = new Point { Line = startLine.LineNumber, Column = span.Start - startLine.Start },
                End = new Point { Line = endLine.LineNumber, Column = span.End - endLine.Start }
            };
        }

        private static void AddRanges(CodeElement.Builder builder, TextSpan attributesSpan, TextSpan fullSpan, TextSpan nameSpan, SourceText text)
        {
            if (attributesSpan != default)
            {
                builder.AddRange(CodeElementRangeNames.Attributes, CreateRange(attributesSpan, text));
            }

            if (fullSpan != default)
            {
                builder.AddRange(CodeElementRangeNames.Full, CreateRange(fullSpan, text));
            }

            if (nameSpan != default)
            {
                builder.AddRange(CodeElementRangeNames.Name, CreateRange(nameSpan, text));
            }
        }

        private void AddSymbolProperties(ISymbol symbol, CodeElement.Builder builder)
        {
            var accessibility = GetAccessibility(symbol);
            if (accessibility != null)
            {
                builder.AddProperty(CodeElementPropertyNames.Accessibility, accessibility);
            }

            builder.AddProperty(CodeElementPropertyNames.Static, symbol.IsStatic);

            foreach (var propertyProvider in _propertyProviders)
            {
                foreach (var (name, value) in propertyProvider.ProvideProperties(symbol))
                {
                    builder.AddProperty(name, value);
                }
            }
        }
    }
}
