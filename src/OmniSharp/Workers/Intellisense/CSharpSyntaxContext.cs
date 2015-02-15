using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OmniSharp
{
    class ReflectionNamespaces
    {
        internal const string WorkspacesAsmName = ", Microsoft.CodeAnalysis.Workspaces, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35";
        internal const string CSWorkspacesAsmName = ", Microsoft.CodeAnalysis.CSharp.Workspaces, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35";
        internal const string CAAsmName = ", Microsoft.CodeAnalysis, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35";
        internal const string CACSharpAsmName = ", Microsoft.CodeAnalysis.CSharp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35";
    }

    class CSharpSyntaxContext
    {
        readonly static Type typeInfo;
        readonly static MethodInfo createContextMethod;
        readonly static PropertyInfo leftTokenProperty;
        readonly static PropertyInfo targetTokenProperty;
        readonly static FieldInfo isIsOrAsTypeContextField;
        readonly static FieldInfo isInstanceContextField;
        readonly static FieldInfo isNonAttributeExpressionContextField;
        readonly static FieldInfo isPreProcessorKeywordContextField;
        readonly static FieldInfo isPreProcessorExpressionContextField;
        readonly static FieldInfo containingTypeDeclarationField;
        readonly static FieldInfo isGlobalStatementContextField;
        readonly static FieldInfo isParameterTypeContextField;
        readonly static PropertyInfo syntaxTreeProperty;

        object instance;

        public SyntaxToken LeftToken
        {
            get
            {
                return (SyntaxToken)leftTokenProperty.GetValue(instance);
            }
        }

        public SyntaxToken TargetToken
        {
            get
            {
                return (SyntaxToken)targetTokenProperty.GetValue(instance);
            }
        }

        public bool IsIsOrAsTypeContext
        {
            get
            {
                return (bool)isIsOrAsTypeContextField.GetValue(instance);
            }
        }

        public bool IsInstanceContext
        {
            get
            {
                return (bool)isInstanceContextField.GetValue(instance);
            }
        }

        public bool IsNonAttributeExpressionContext
        {
            get
            {
                return (bool)isNonAttributeExpressionContextField.GetValue(instance);
            }
        }

        public bool IsPreProcessorKeywordContext
        {
            get
            {
                return (bool)isPreProcessorKeywordContextField.GetValue(instance);
            }
        }

        public bool IsPreProcessorExpressionContext
        {
            get
            {
                return (bool)isPreProcessorExpressionContextField.GetValue(instance);
            }
        }

        public TypeDeclarationSyntax ContainingTypeDeclaration
        {
            get
            {
                return (TypeDeclarationSyntax)containingTypeDeclarationField.GetValue(instance);
            }
        }

        public bool IsGlobalStatementContext
        {
            get
            {
                return (bool)isGlobalStatementContextField.GetValue(instance);
            }
        }

        public bool IsParameterTypeContext
        {
            get
            {
                return (bool)isParameterTypeContextField.GetValue(instance);
            }
        }

        public SyntaxTree SyntaxTree
        {
            get
            {
                return (SyntaxTree)syntaxTreeProperty.GetValue(instance);
            }
        }

        static CSharpSyntaxContext()
        {
            typeInfo = Type.GetType("Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery.CSharpSyntaxContext" + ReflectionNamespaces.CSWorkspacesAsmName, true);

            createContextMethod = typeInfo.GetMethod("CreateContext", BindingFlags.Static | BindingFlags.Public);
            leftTokenProperty = typeInfo.GetProperty("LeftToken");
            targetTokenProperty = typeInfo.GetProperty("TargetToken");
            isIsOrAsTypeContextField = typeInfo.GetField("IsIsOrAsTypeContext");
            isInstanceContextField = typeInfo.GetField("IsInstanceContext");
            isNonAttributeExpressionContextField = typeInfo.GetField("IsNonAttributeExpressionContext");
            isPreProcessorKeywordContextField = typeInfo.GetField("IsPreProcessorKeywordContext");
            isPreProcessorExpressionContextField = typeInfo.GetField("IsPreProcessorExpressionContext");
            containingTypeDeclarationField = typeInfo.GetField("ContainingTypeDeclaration");
            isGlobalStatementContextField = typeInfo.GetField("IsGlobalStatementContext");
            isParameterTypeContextField = typeInfo.GetField("IsParameterTypeContext");
            syntaxTreeProperty = typeInfo.GetProperty("SyntaxTree");
        }

        CSharpSyntaxContext(object instance)
        {
            this.instance = instance;
        }

        internal static CSharpSyntaxContext CreateContext(Workspace workspace, SemanticModel semanticModel, int position, CancellationToken cancellationToken = default(CancellationToken))
        {
            return new CSharpSyntaxContext(createContextMethod.Invoke(null, new object[] { workspace, semanticModel, position, cancellationToken }));
        }
    }

    class CSharpTypeInferenceService
    {
        readonly static Type typeInfo;
        readonly static MethodInfo inferTypesMethod;
        readonly static MethodInfo inferTypes2Method;
        readonly object instance;

        static CSharpTypeInferenceService()
        {
            typeInfo = Type.GetType("Microsoft.CodeAnalysis.CSharp.CSharpTypeInferenceService" + ReflectionNamespaces.CSWorkspacesAsmName, true);

            inferTypesMethod = typeInfo.GetMethod("InferTypes", new[] { typeof(SemanticModel), typeof(int), typeof(CancellationToken) });
            inferTypes2Method = typeInfo.GetMethod("InferTypes", new[] { typeof(SemanticModel), typeof(SyntaxNode), typeof(CancellationToken) });
        }

        public CSharpTypeInferenceService()
        {
            instance = Activator.CreateInstance(typeInfo);
        }

        public IEnumerable<ITypeSymbol> InferTypes(SemanticModel semanticModel, int position, CancellationToken cancellationToken)
        {
            return (IEnumerable<ITypeSymbol>)inferTypesMethod.Invoke(instance, new object[] { semanticModel, position, cancellationToken });
        }

        public IEnumerable<ITypeSymbol> InferTypes(SemanticModel semanticModel, SyntaxNode expression, CancellationToken cancellationToken)
        {
            return (IEnumerable<ITypeSymbol>)inferTypes2Method.Invoke(instance, new object[] { semanticModel, expression, cancellationToken });
        }
    }

    class CaseCorrector
    {
        readonly static Type typeInfo;
        readonly static MethodInfo caseCorrectAsyncMethod;

        static CaseCorrector()
        {
            typeInfo = Type.GetType("Microsoft.CodeAnalysis.CaseCorrection.CaseCorrector" + ReflectionNamespaces.WorkspacesAsmName, true);

            Annotation = (SyntaxAnnotation)typeInfo.GetField("Annotation", BindingFlags.Public | BindingFlags.Static).GetValue(null);

            caseCorrectAsyncMethod = typeInfo.GetMethod("CaseCorrectAsync", new[] { typeof(Document), typeof(SyntaxAnnotation), typeof(CancellationToken) });
        }

        public static readonly SyntaxAnnotation Annotation;

        public static Task<Document> CaseCorrectAsync(Document document, SyntaxAnnotation annotation, CancellationToken cancellationToken)
        {
            return (Task<Document>)caseCorrectAsyncMethod.Invoke(null, new object[] { document, annotation, cancellationToken });
        }
    }

    class SpeculationAnalyzer
    {
        readonly static Type typeInfo;
        readonly static MethodInfo symbolsForOriginalAndReplacedNodesAreCompatibleMethod;
        readonly static MethodInfo replacementChangesSemanticsMethod;
        readonly object instance;

        static SpeculationAnalyzer()
        {
            Type[] abstractSpeculationAnalyzerGenericParams = new[]
            {
                Type.GetType("Microsoft.CodeAnalysis.SyntaxNode" + ReflectionNamespaces.CAAsmName, true),
                Type.GetType("Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax" + ReflectionNamespaces.CACSharpAsmName, true),
                Type.GetType("Microsoft.CodeAnalysis.CSharp.Syntax.TypeSyntax" + ReflectionNamespaces.CACSharpAsmName, true),
                Type.GetType("Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax" + ReflectionNamespaces.CACSharpAsmName, true),
                Type.GetType("Microsoft.CodeAnalysis.CSharp.Syntax.ArgumentSyntax" + ReflectionNamespaces.CACSharpAsmName, true),
                Type.GetType("Microsoft.CodeAnalysis.CSharp.Syntax.ForEachStatementSyntax" + ReflectionNamespaces.CACSharpAsmName, true),
                Type.GetType("Microsoft.CodeAnalysis.CSharp.Syntax.ThrowStatementSyntax" + ReflectionNamespaces.CACSharpAsmName, true),
                Type.GetType("Microsoft.CodeAnalysis.SemanticModel" + ReflectionNamespaces.CAAsmName, true)
            };
            typeInfo = Type.GetType("Microsoft.CodeAnalysis.Shared.Utilities.AbstractSpeculationAnalyzer`8" + ReflectionNamespaces.WorkspacesAsmName, true)
                .MakeGenericType(abstractSpeculationAnalyzerGenericParams);

            symbolsForOriginalAndReplacedNodesAreCompatibleMethod = typeInfo.GetMethod("SymbolsForOriginalAndReplacedNodesAreCompatible", BindingFlags.Public | BindingFlags.Instance);
            replacementChangesSemanticsMethod = typeInfo.GetMethod("ReplacementChangesSemantics", BindingFlags.Public | BindingFlags.Instance);

            typeInfo = Type.GetType("Microsoft.CodeAnalysis.CSharp.Utilities.SpeculationAnalyzer" + ReflectionNamespaces.CSWorkspacesAsmName, true);
        }

        public SpeculationAnalyzer(ExpressionSyntax expression, ExpressionSyntax newExpression, SemanticModel semanticModel, CancellationToken cancellationToken, bool skipVerificationForReplacedNode = false, bool failOnOverloadResolutionFailuresInOriginalCode = false)
        {
            instance = Activator.CreateInstance(typeInfo, new object[] { expression, newExpression, semanticModel, cancellationToken, skipVerificationForReplacedNode, failOnOverloadResolutionFailuresInOriginalCode });
        }

        public bool SymbolsForOriginalAndReplacedNodesAreCompatible()
        {
            return (bool)symbolsForOriginalAndReplacedNodesAreCompatibleMethod.Invoke(instance, new object[0]);
        }

        public bool ReplacementChangesSemantics()
        {
            return (bool)replacementChangesSemanticsMethod.Invoke(instance, new object[0]);
        }
    }
}