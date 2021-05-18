// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
// adapted from Microsoft.CodeAnalysis.CSharp.EditorFeatures

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.MetadataAsSource;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.CSharp.DocumentationComments;

namespace OmniSharp.Roslyn.CSharp.Services.Decompilation
{
    public class OmniSharpCSharpDecompiledSourceService
    {
        private readonly ILoggerFactory _loggerFactory;
        private static readonly FileVersionInfo decompilerVersion = FileVersionInfo.GetVersionInfo(typeof(CSharpDecompiler).Assembly.Location);

        public OmniSharpCSharpDecompiledSourceService(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public async Task<Document> AddSourceToAsync(Document document, Compilation symbolCompilation, Microsoft.CodeAnalysis.ISymbol symbol, CancellationToken cancellationToken)
        {
            // Get the name of the type the symbol is in
            var containingOrThis = symbol.GetContainingTypeOrThis();
            var fullName = GetFullReflectionName(containingOrThis);

            var reference = symbolCompilation.GetMetadataReference(symbol.ContainingAssembly);
            var assemblyLocation = (reference as PortableExecutableReference)?.FilePath;
            if (assemblyLocation == null)
            {
                throw new NotSupportedException("Cannot_navigate_to_the_symbol_under_the_caret");
            }

            // Decompile
            document = PerformDecompilation(document, fullName, symbolCompilation, assemblyLocation);

            document = await AddAssemblyInfoRegionAsync(document, symbol, cancellationToken).ConfigureAwait(false);

            // Convert XML doc comments to regular comments, just like MAS
            document = await ConvertDocCommentsToRegularCommentsAsync(document, cancellationToken).ConfigureAwait(false);

            var node = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            // Apply formatting rules
            document = await Formatter.FormatAsync(
                  document, new[] { node.FullSpan },
                  options: null, cancellationToken).ConfigureAwait(false);

            return document;
        }

        private Document PerformDecompilation(Document document, string fullName, Compilation compilation, string assemblyLocation)
        {
            // Load the assembly.
            var file = new PEFile(assemblyLocation, PEStreamOptions.PrefetchEntireImage);

            // Initialize a decompiler with default settings.
            var decompiler = new CSharpDecompiler(file, new AssemblyResolver(compilation, _loggerFactory), new DecompilerSettings());
            // Escape invalid identifiers to prevent Roslyn from failing to parse the generated code.
            // (This happens for example, when there is compiler-generated code that is not yet recognized/transformed by the decompiler.)
            decompiler.AstTransforms.Add(new EscapeInvalidIdentifiers());

            var fullTypeName = new FullTypeName(fullName);

            // Try to decompile; if an exception is thrown the caller will handle it
            var text = decompiler.DecompileTypeAsString(fullTypeName);
            return document.WithText(SourceText.From(text));
        }

        private async Task<Document> AddAssemblyInfoRegionAsync(Document document, Microsoft.CodeAnalysis.ISymbol symbol, CancellationToken cancellationToken)
        {
            var assemblyInfo = OmniSharpMetadataAsSourceHelpers.GetAssemblyInfo(symbol.ContainingAssembly);
            var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            var assemblyPath = OmniSharpMetadataAsSourceHelpers.GetAssemblyDisplay(compilation, symbol.ContainingAssembly);

            var regionTrivia = SyntaxFactory.RegionDirectiveTrivia(true)
                .WithTrailingTrivia(new[] { SyntaxFactory.Space, SyntaxFactory.PreprocessingMessage(assemblyInfo) });

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = oldRoot.WithLeadingTrivia(new[]
                {
                    SyntaxFactory.Trivia(regionTrivia),
                    SyntaxFactory.CarriageReturnLineFeed,
                    SyntaxFactory.Comment("// " + assemblyPath),
                    SyntaxFactory.CarriageReturnLineFeed,
                    SyntaxFactory.Comment($"// Decompiled with ICSharpCode.Decompiler {decompilerVersion.FileVersion}"),
                    SyntaxFactory.CarriageReturnLineFeed,
                    SyntaxFactory.Trivia(SyntaxFactory.EndRegionDirectiveTrivia(true)),
                    SyntaxFactory.CarriageReturnLineFeed,
                    SyntaxFactory.CarriageReturnLineFeed
                });

            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> ConvertDocCommentsToRegularCommentsAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newSyntaxRoot = OmniSharpDocCommentConverter.ConvertToRegularComments(syntaxRoot, document.Project, cancellationToken);
            return document.WithSyntaxRoot(newSyntaxRoot);
        }

        private string GetFullReflectionName(INamedTypeSymbol containingType)
        {
            var stack = new Stack<string>();
            stack.Push(containingType.MetadataName);
            var ns = containingType.ContainingNamespace;
            do
            {
                stack.Push(ns.Name);
                ns = ns.ContainingNamespace;
            }
            while (ns != null && !ns.IsGlobalNamespace);

            return string.Join(".", stack);
        }
    }
}
