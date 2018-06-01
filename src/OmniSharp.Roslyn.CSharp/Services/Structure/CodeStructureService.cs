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

namespace OmniSharp.Roslyn.CSharp.Services.Structure
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.CodeStructure, LanguageNames.CSharp)]
    public class CodeStructureService : IRequestHandler<CodeStructureRequest, CodeStructureResponse>
    {
        private static readonly SymbolDisplayFormat s_TypeFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance);

        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public CodeStructureService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
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

        private static async Task<CodeElement[]> GetCodeElementsAsync(Document document)
        {
            var text = await document.GetTextAsync();
            var syntaxRoot = await document.GetSyntaxRootAsync();
            var semanticModel = await document.GetSemanticModelAsync();

            return GetCodeElements(syntaxRoot.ChildNodes(), semanticModel, text);
        }

        private static CodeElement[] GetCodeElements(IEnumerable<SyntaxNode> nodes, SemanticModel semanticModel, SourceText text)
        {
            List<CodeElement> results = null;

            foreach (var node in nodes)
            {
                switch (node)
                {
                    case BaseTypeDeclarationSyntax baseTypeDeclaration:
                        Add(GetCodeElement(baseTypeDeclaration, text, semanticModel));
                        break;
                    case DelegateDeclarationSyntax delegateDeclaration:
                        Add(GetCodeElement(delegateDeclaration, text, semanticModel));
                        break;
                    case NamespaceDeclarationSyntax namespaceDeclaration:
                        Add(GetCodeElement(namespaceDeclaration, text, semanticModel));
                        break;
                }
            }

            return results != null
                ? results.ToArray()
                : null;

            void Add(CodeElement element)
            {
                if (element == null)
                {
                    return;
                }

                if (results == null)
                {
                    results = new List<CodeElement>();
                }

                results.Add(element);
            }
        }

        private static CodeElement GetCodeElement(BaseTypeDeclarationSyntax baseTypeDeclaration, SourceText text, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(baseTypeDeclaration);
            if (symbol == null)
            {
                return null;
            }

            var ranges = new List<CodeElementRange>();
            AddRange(ranges, CodeElementRangeKinds.Attributes, baseTypeDeclaration.AttributeLists.Span, text);
            AddRange(ranges, CodeElementRangeKinds.Full, baseTypeDeclaration.Span, text);
            AddRange(ranges, CodeElementRangeKinds.Name, baseTypeDeclaration.Identifier.Span, text);

            var propertyBuilder = ImmutableDictionary.CreateBuilder<string, object>();

            var accessibility = GetAccessibility(symbol);
            if (accessibility != null)
            {
                propertyBuilder.Add(CodeElementPropertyNames.Accessibility, accessibility);
            }

            propertyBuilder.Add(CodeElementPropertyNames.Static, symbol.IsStatic);

            return new CodeElement
            {
                Kind = GetKind(baseTypeDeclaration, symbol),
                Name = baseTypeDeclaration.Identifier.ToString(),
                DisplayName = symbol.ToDisplayString(s_TypeFormat),
                Children = GetCodeElements(baseTypeDeclaration.ChildNodes(), semanticModel, text),
                Ranges = ranges.ToArray(),
                Properties = propertyBuilder.ToImmutable()
            };
        }

        private static CodeElement GetCodeElement(DelegateDeclarationSyntax delegateDeclaration, SourceText text, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(delegateDeclaration);
            if (symbol == null)
            {
                return null;
            }

            var ranges = new List<CodeElementRange>();
            AddRange(ranges, CodeElementRangeKinds.Attributes, delegateDeclaration.AttributeLists.Span, text);
            AddRange(ranges, CodeElementRangeKinds.Full, delegateDeclaration.Span, text);
            AddRange(ranges, CodeElementRangeKinds.Name, delegateDeclaration.Identifier.Span, text);

            var propertyBuilder = ImmutableDictionary.CreateBuilder<string, object>();

            var accessibility = GetAccessibility(symbol);
            if (accessibility != null)
            {
                propertyBuilder.Add(CodeElementPropertyNames.Accessibility, accessibility);
            }

            propertyBuilder.Add(CodeElementPropertyNames.Static, symbol.IsStatic);

            return new CodeElement
            {
                Kind = GetKind(delegateDeclaration, symbol),
                Name = delegateDeclaration.Identifier.ToString(),
                DisplayName = symbol.ToDisplayString(s_TypeFormat),
                Ranges = ranges.ToArray(),
                Properties = propertyBuilder.ToImmutable()
            };
        }

        private static CodeElement GetCodeElement(NamespaceDeclarationSyntax namespaceDeclaration, SourceText text, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(namespaceDeclaration);
            if (symbol == null)
            {
                return null;
            }

            var ranges = new List<CodeElementRange>();
            AddRange(ranges, CodeElementRangeKinds.Full, namespaceDeclaration.Span, text);
            AddRange(ranges, CodeElementRangeKinds.Name, namespaceDeclaration.Name.Span, text);

            return new CodeElement
            {
                Kind = GetKind(namespaceDeclaration, symbol),
                Name = namespaceDeclaration.Name.ToString(),
                DisplayName = symbol.ToDisplayString(s_TypeFormat),
                Children = GetCodeElements(namespaceDeclaration.ChildNodes(), semanticModel, text),
                Ranges = ranges.ToArray()
            };
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

        private static void AddRange(List<CodeElementRange> rangeList, string name, TextSpan span, SourceText text)
        {
            if (span != default)
            {
                rangeList.Add(CreateRange(name, span, text));
            }
        }

        private static CodeElementRange CreateRange(string name, TextSpan span, SourceText text)
        {
            var startLine = text.Lines.GetLineFromPosition(span.Start);
            var endLine = text.Lines.GetLineFromPosition(span.End);

            return new CodeElementRange
            {
                Name = name,
                Range = new Range
                {
                    Start = new Point { Line = startLine.LineNumber, Column = span.Start - startLine.Start },
                    End = new Point { Line = endLine.LineNumber, Column = span.End - endLine.Start }
                }
            };
        }
    }
}
