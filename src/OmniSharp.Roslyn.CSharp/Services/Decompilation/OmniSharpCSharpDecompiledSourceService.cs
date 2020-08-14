// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
// adapted from Microsoft.CodeAnalysis.CSharp.EditorFeatures

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions;
using System.IO;
using Microsoft.CodeAnalysis;
using OmniSharp.Services;
using System.Reflection;
using OmniSharp.Utilities;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OmniSharp.Roslyn.CSharp.Services.Decompilation
{
    public class OmniSharpCSharpDecompiledSourceService
    {
        private readonly ILoggerFactory _loggerFactory;
        private const string MetadataAsSourceHelpers = "Microsoft.CodeAnalysis.MetadataAsSource.MetadataAsSourceHelpers";
        private const string CSharpDocumentationCommentFormattingService = "Microsoft.CodeAnalysis.CSharp.DocumentationComments.CSharpDocumentationCommentFormattingService";
        private const string DocCommentConverter = "Microsoft.CodeAnalysis.CSharp.DocumentationComments.DocCommentConverter";
        private static readonly FileVersionInfo decompilerVersion = FileVersionInfo.GetVersionInfo(typeof(CSharpDecompiler).Assembly.Location);
        private readonly Lazy<Assembly> _roslynFeatureAssembly;
        private readonly Lazy<Assembly> _csharpFeatureAssembly;
        private readonly Lazy<Type> _csharpMetadataAsSourceService;
        private readonly Lazy<Type> _csharpDocumentationCommentFormattingService;
        private readonly Lazy<Type> _docCommentConverter;
        private readonly Lazy<MethodInfo> _metadataGetAssemblyInfo;
        private readonly Lazy<MethodInfo> _metadataGetAssemblyDisplay;

        public OmniSharpCSharpDecompiledSourceService(IAssemblyLoader loader, ILoggerFactory loggerFactory)
        {
            _roslynFeatureAssembly = loader.LazyLoad(Configuration.RoslynFeatures);
            _csharpFeatureAssembly = loader.LazyLoad(Configuration.RoslynCSharpFeatures);
            _csharpMetadataAsSourceService = _roslynFeatureAssembly.LazyGetType(MetadataAsSourceHelpers);
            _csharpDocumentationCommentFormattingService = _csharpFeatureAssembly.LazyGetType(CSharpDocumentationCommentFormattingService);
            _docCommentConverter = _csharpFeatureAssembly.LazyGetType(DocCommentConverter);
            _metadataGetAssemblyInfo = _csharpMetadataAsSourceService.LazyGetMethod("GetAssemblyInfo");
            _metadataGetAssemblyDisplay = _csharpMetadataAsSourceService.LazyGetMethod("GetAssemblyDisplay");

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
            var docCommentFormattingService = _csharpDocumentationCommentFormattingService.CreateInstance();
            document = await ConvertDocCommentsToRegularCommentsAsync(document, docCommentFormattingService, cancellationToken).ConfigureAwait(false);

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
            var assemblyInfo = _metadataGetAssemblyInfo.Value.InvokeStatic<string>(new object[] { symbol.ContainingAssembly });
            var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            var assemblyPath = _metadataGetAssemblyDisplay.Value.InvokeStatic<string>(new object[] { compilation, symbol.ContainingAssembly });

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

        private async Task<Document> ConvertDocCommentsToRegularCommentsAsync(Document document, object docCommentFormattingService, CancellationToken cancellationToken)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var newSyntaxRoot = _docCommentConverter.InvokeStatic<SyntaxNode>("ConvertToRegularComments", new object[] { syntaxRoot, docCommentFormattingService, cancellationToken });

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
