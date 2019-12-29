// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
using Microsoft.CodeAnalysis.CSharp.DocumentationComments;
using Microsoft.CodeAnalysis.DocumentationComments;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.MetadataAsSource;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using OmniSharp.Extensions;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;

namespace OmniSharp.Roslyn.CSharp.Services.Decompilation
{
    public class OmniSharpCSharpDecompiledSourceService
    {
        private readonly HostLanguageServices provider;
        private static readonly FileVersionInfo decompilerVersion = FileVersionInfo.GetVersionInfo(typeof(CSharpDecompiler).Assembly.Location);

        public OmniSharpCSharpDecompiledSourceService(HostLanguageServices provider)
        {
            this.provider = provider;
        }

        public async Task<Document> AddSourceToAsync(Document document, Compilation symbolCompilation, Microsoft.CodeAnalysis.ISymbol symbol, CancellationToken cancellationToken)
        {
            // Get the name of the type the symbol is in
            var containingOrThis = symbol.GetContainingTypeOrThis();
            var fullName = GetFullReflectionName(containingOrThis);

            string assemblyLocation = null;
            var isReferenceAssembly = symbol.ContainingAssembly.GetAttributes().Any(attribute => attribute.AttributeClass.Name == nameof(ReferenceAssemblyAttribute)
                && attribute.AttributeClass.ToNameDisplayString() == typeof(ReferenceAssemblyAttribute).FullName);
            if (isReferenceAssembly)
            {
                try
                {
                    var fullAssemblyName = symbol.ContainingAssembly.Identity.GetDisplayName();

                    var globalAssemblyCacheType = typeof(ScriptOptions).Assembly.GetType("Microsoft.CodeAnalysis.GlobalAssemblyCache");
                    var instance = globalAssemblyCacheType.GetField("Instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);

                    //var resolvePartialNameMethod = instance.GetType().GetMethod("ResolvePartialName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

                    var args = new object[] { fullAssemblyName, assemblyLocation, null, CultureInfo.CurrentCulture };
                    // resolvePartialNameMethod.Invoke(instance, args);
                    instance.GetType().InvokeMember("ResolvePartialName", System.Reflection.BindingFlags.InvokeMethod, Type.DefaultBinder, instance, args);
                    assemblyLocation = (string)args[1];
                    //GlobalAssemblyCache.Instance.ResolvePartialName(fullAssemblyName, out assemblyLocation, preferredCulture: CultureInfo.CurrentCulture);
                }
                catch (Exception)
                {
                    // log
                }
            }

            if (assemblyLocation == null)
            {
                var reference = symbolCompilation.GetMetadataReference(symbol.ContainingAssembly);
                assemblyLocation = (reference as PortableExecutableReference)?.FilePath;
                if (assemblyLocation == null)
                {
                    throw new NotSupportedException("Cannot_navigate_to_the_symbol_under_the_caret");
                }
            }

            // Decompile
            document = PerformDecompilation(document, fullName, symbolCompilation, assemblyLocation);

            //document = await AddAssemblyInfoRegionAsync(document, symbol, cancellationToken).ConfigureAwait(false);

            // Convert XML doc comments to regular comments, just like MAS
            //var docCommentFormattingService = document.GetLanguageService<IDocumentationCommentFormattingService>();
            //document = await ConvertDocCommentsToRegularCommentsAsync(document, docCommentFormattingService, cancellationToken).ConfigureAwait(false);

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
            var decompiler = new CSharpDecompiler(file, new AssemblyResolver(compilation), new DecompilerSettings());
            // Escape invalid identifiers to prevent Roslyn from failing to parse the generated code.
            // (This happens for example, when there is compiler-generated code that is not yet recognized/transformed by the decompiler.)
            decompiler.AstTransforms.Add(new EscapeInvalidIdentifiers());

            var fullTypeName = new FullTypeName(fullName);

            // Try to decompile; if an exception is thrown the caller will handle it
            var text = decompiler.DecompileTypeAsString(fullTypeName);
            return document.WithText(SourceText.From(text));
        }

        //private async Task<Document> AddAssemblyInfoRegionAsync(Document document, ISymbol symbol, CancellationToken cancellationToken)
        //{
        //    var assemblyInfo = MetadataAsSourceHelpers.GetAssemblyInfo(symbol.ContainingAssembly);
        //    var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        //    var assemblyPath = MetadataAsSourceHelpers.GetAssemblyDisplay(compilation, symbol.ContainingAssembly);

        //    var regionTrivia = SyntaxFactory.RegionDirectiveTrivia(true)
        //        .WithTrailingTrivia(new[] { SyntaxFactory.Space, SyntaxFactory.PreprocessingMessage(assemblyInfo) });

        //    var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        //    var newRoot = oldRoot.WithLeadingTrivia(new[]
        //        {
        //            SyntaxFactory.Trivia(regionTrivia),
        //            SyntaxFactory.CarriageReturnLineFeed,
        //            SyntaxFactory.Comment("// " + assemblyPath),
        //            SyntaxFactory.CarriageReturnLineFeed,
        //            SyntaxFactory.Comment($"// Decompiled with ICSharpCode.Decompiler {decompilerVersion.FileVersion}"),
        //            SyntaxFactory.CarriageReturnLineFeed,
        //            SyntaxFactory.Trivia(SyntaxFactory.EndRegionDirectiveTrivia(true)),
        //            SyntaxFactory.CarriageReturnLineFeed,
        //            SyntaxFactory.CarriageReturnLineFeed
        //        });

        //    return document.WithSyntaxRoot(newRoot);
        //}

        //private async Task<Document> ConvertDocCommentsToRegularCommentsAsync(Document document, IDocumentationCommentFormattingService docCommentFormattingService, CancellationToken cancellationToken)
        //{
        //    var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        //    var newSyntaxRoot = DocCommentConverter.ConvertToRegularComments(syntaxRoot, docCommentFormattingService, cancellationToken);

        //    return document.WithSyntaxRoot(newSyntaxRoot);
        //}

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

    internal class AssemblyResolver : IAssemblyResolver
    {
        private readonly Compilation parentCompilation;
        private static readonly Version zeroVersion = new Version(0, 0, 0, 0);

        public AssemblyResolver(Compilation parentCompilation)
        {
            this.parentCompilation = parentCompilation;
        }

        public PEFile Resolve(IAssemblyReference name)
        {
            var assemblies = parentCompilation.Assembly.Modules.First().ReferencedAssemblySymbols;
            //foreach (var assembly in parentCompilation.GetReferencedAssemblySymbols())
            foreach (var assembly in assemblies)
            {
                // First, find the correct IAssemblySymbol by name and PublicKeyToken.
                if (assembly.Identity.Name != name.Name
                    || !assembly.Identity.PublicKeyToken.SequenceEqual(name.PublicKeyToken ?? Array.Empty<byte>()))
                {
                    continue;
                }

                // Normally we skip versions that do not match, except if the reference is "mscorlib" (see comments below)
                // or if the name.Version is '0.0.0.0'. This is because we require the metadata of all transitive references
                // and modules, to achieve best decompilation results.
                // In the case of .NET Standard projects for example, the 'netstandard' reference contains no references
                // with actual versions. All versions are '0.0.0.0', therefore we have to ignore those version numbers,
                // and can just use the references provided by Roslyn instead.
                if (assembly.Identity.Version != name.Version && name.Version != zeroVersion
                    && !string.Equals("mscorlib", assembly.Identity.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // MSBuild treats mscorlib special for the purpose of assembly resolution/unification, where all
                    // versions of the assembly are considered equal. The same policy is adopted here.
                    continue;
                }

                // reference assemblies should be fine here, we only need the metadata of references.
                var reference = parentCompilation.GetMetadataReference(assembly);
                var path = (string)reference.GetType().GetProperty("FilePath", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.GetValue(reference);
                return new PEFile(path ?? reference.Display, PEStreamOptions.PrefetchMetadata);
            }

            // not found
            return null;
        }

        public PEFile ResolveModule(PEFile mainModule, string moduleName)
        {
            // Primitive implementation to support multi-module assemblies
            // where all modules are located next to the main module.
            var baseDirectory = Path.GetDirectoryName(mainModule.FileName);
            var moduleFileName = Path.Combine(baseDirectory, moduleName);
            if (!File.Exists(moduleFileName))
            {
                return null;
            }

            return new PEFile(moduleFileName, PEStreamOptions.PrefetchMetadata);
        }
    }
}
