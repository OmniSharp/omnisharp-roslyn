//
// KeywordContextHandler.cs
//
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OmniSharp
{
    class KeywordContextHandler
    {
        public IEnumerable<string> Get(CSharpSyntaxContext ctx, SemanticModel semanticModel, int offset, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = new List<string>();
            var parent = ctx.TargetToken.Parent;
            if (parent != null && parent.IsKind(SyntaxKind.ArrayRankSpecifier))
                return result;
            if (ctx.IsIsOrAsTypeContext)
            {
                foreach (var kw in primitiveTypesKeywords)
                    result.Add(kw);
                return result;
            }
            if (parent != null)
            {
                if (parent.Kind() == SyntaxKind.IdentifierName)
                {
                    if (ctx.LeftToken.Parent.Kind() == SyntaxKind.IdentifierName &&
                        parent.Parent != null && parent.Parent.Kind() == SyntaxKind.ParenthesizedExpression ||
                        ctx.LeftToken.Parent.Kind() == SyntaxKind.CatchDeclaration)
                        return result;
                }
                if (parent.Kind() == SyntaxKind.NamespaceDeclaration)
                {
                    var decl = parent as NamespaceDeclarationSyntax;
                    if (decl.OpenBraceToken.Span.Length > 0 &&
                        decl.OpenBraceToken.SpanStart > ctx.TargetToken.SpanStart)
                        return result;
                }
                if (parent.Kind() == SyntaxKind.ClassDeclaration ||
                    parent.Kind() == SyntaxKind.StructDeclaration ||
                    parent.Kind() == SyntaxKind.InterfaceDeclaration)
                {
                    foreach (var kw in typeLevelKeywords)
                        result.Add(kw);
                    return result;
                }
                if (parent.Kind() == SyntaxKind.EnumDeclaration ||
                    parent.Kind() == SyntaxKind.DelegateDeclaration ||
                    parent.Kind() == SyntaxKind.PredefinedType ||
                    parent.Kind() == SyntaxKind.TypeParameterList ||
                    parent.Kind() == SyntaxKind.QualifiedName ||
                    parent.Kind() == SyntaxKind.SimpleMemberAccessExpression)
                {
                    return result;
                }
            }
            if (parent.IsKind(SyntaxKind.AttributeList))
            {
                if (parent.Parent.Parent == null || parent.Parent.Parent.IsKind(SyntaxKind.CompilationUnit))
                {
                    result.Add("assembly");
                    result.Add("module");
                    result.Add("type");
                }
                else
                {
                    result.Add("param");
                    result.Add("field");
                    result.Add("property");
                    result.Add("method");
                    result.Add("event");
                }
                result.Add("return");
            }
            if (ctx.IsInstanceContext)
            {
                if (ctx.LeftToken.Parent.Ancestors().Any(a => a is SwitchStatementSyntax || a is BlockSyntax && a.ToFullString().IndexOf("switch", StringComparison.Ordinal) > 0))
                {
                    result.Add("case");
                }
            }

            var forEachStatementSyntax = parent as ForEachStatementSyntax;
            if (forEachStatementSyntax != null)
            {
                if (forEachStatementSyntax.Type.Span.Length > 0 &&
                    forEachStatementSyntax.Identifier.Span.Length > 0 &&
                    forEachStatementSyntax.InKeyword.Span.Length == 0)
                {
                    result.Add("in");
                    return result;
                }
            }
            if (parent != null && parent.Kind() == SyntaxKind.ArgumentList)
            {
                result.Add("out");
                result.Add("ref");
            }
            else if (parent != null && parent.Kind() == SyntaxKind.ParameterList)
            {
                result.Add("out");
                result.Add("ref");
                result.Add("params");
                foreach (var kw in primitiveTypesKeywords)
                    result.Add(kw);

                if (ctx.IsParameterTypeContext)
                {
                    bool isFirst = ctx.LeftToken.GetPreviousToken().IsKind(SyntaxKind.OpenParenToken);
                    if (isFirst)
                        result.Add("this");
                }

                return result;
            }
            else
            {
                result.Add("var");
                result.Add("dynamic");
            }

            if (parent != null && parent.Parent != null && parent.IsKind(SyntaxKind.BaseList) && parent.Parent.IsKind(SyntaxKind.EnumDeclaration))
            {
                foreach (var kw in validEnumBaseTypes)
                    result.Add(kw);
                return result;
            }
            if (parent != null &&
                parent.Parent != null &&
                parent.Parent.IsKind(SyntaxKind.FromClause))
            {
                foreach (var kw in linqKeywords)
                    result.Add(kw);
            }
            if (ctx.IsGlobalStatementContext || parent == null || parent is NamespaceDeclarationSyntax)
            {
                foreach (var kw in globalLevelKeywords)
                    result.Add(kw);
                return result;
            }
            else
            {
                foreach (var kw in typeLevelKeywords)
                    result.Add(kw);
            }

            foreach (var kw in primitiveTypesKeywords)
                result.Add(kw);

            foreach (var kw in statementStartKeywords)
                result.Add(kw);

            foreach (var kw in expressionLevelKeywords)
                result.Add(kw);

            if (ctx.IsPreProcessorKeywordContext)
            {
                foreach (var kw in preprocessorKeywords)
                    result.Add(kw);
            }

            if (ctx.IsPreProcessorExpressionContext)
            {
                var parseOptions = semanticModel.SyntaxTree.Options as CSharpParseOptions;
                foreach (var define in parseOptions.PreprocessorSymbolNames)
                {
                    result.Add(define);
                }
            }
            if (parent.IsKind(SyntaxKind.TypeParameterConstraintClause))
            {
                result.Add("new()");
            }
            return result;
        }

        static readonly string[] preprocessorKeywords = {
            "else",
            "elif",
            "endif",
            "define",
            "undef",
            "warning",
            "error",
            "pragma",
            "line",
            "line hidden",
            "line default",
            "region",
            "endregion"
        };

        static readonly string[] validEnumBaseTypes = {
            "byte",
            "sbyte",
            "short",
            "int",
            "long",
            "ushort",
            "uint",
            "ulong"
        };

        static readonly string[] expressionLevelKeywords = {
            "as",
            "is",
            "else",
            "out",
            "ref",
            "null",
            "delegate",
            "default",
            "true",
            "false"
        };

        static readonly string[] primitiveTypesKeywords = {
            "void",
            "object",
            "bool",
            "byte",
            "sbyte",
            "char",
            "short",
            "int",
            "long",
            "ushort",
            "uint",
            "ulong",
            "float",
            "double",
            "decimal",
            "string"
        };

        static readonly string[] statementStartKeywords = {
            "base", "new", "sizeof", "this",
            "true", "false", "typeof", "checked", "unchecked", "from", "break", "checked",
            "unchecked", "const", "continue", "do", "finally", "fixed", "for", "foreach",
            "goto", "if", "lock", "return", "stackalloc", "switch", "throw", "try", "unsafe",
            "using", "while", "yield",
            "catch"
        };

        static readonly string[] globalLevelKeywords = {
            "namespace", "using", "extern", "public", "internal",
            "class", "interface", "struct", "enum", "delegate",
            "abstract", "sealed", "static", "unsafe", "partial"
        };

        static readonly string[] typeLevelKeywords = {
            "public", "internal", "protected", "private", "async",
            "class", "interface", "struct", "enum", "delegate",
            "abstract", "sealed", "static", "unsafe", "partial",
            "const", "event", "extern", "fixed", "new",
            "operator", "explicit", "implicit",
            "override", "readonly", "virtual", "volatile"
        };

        static readonly string[] linqKeywords = {
            "from",
            "where",
            "select",
            "group",
            "into",
            "orderby",
            "join",
            "let",
            "in",
            "on",
            "equals",
            "by",
            "ascending",
            "descending"
        };

        static readonly string[] parameterTypePredecessorKeywords = {
            "out",
            "ref",
            "params"
        };
    }

}