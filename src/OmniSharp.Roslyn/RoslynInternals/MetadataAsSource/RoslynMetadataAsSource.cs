#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Roslyn.Reflection;
using OmniSharp.Roslyn.RoslynInternals.Formatting;

namespace OmniSharp.Roslyn.RoslynInternals.MetadataAsSource
{
    public static class RoslynMetadataAsSourceHelpers
    {
        private static readonly Type s_type = RoslynReflection.GetType(
            RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.MetadataAsSource.MetadataAsSourceHelpers");

        public static string GetAssemblyInfo(IAssemblySymbol assemblySymbol)
            => RoslynReflection.Invoke<string>(
                RoslynReflection.GetMethod(s_type, "GetAssemblyInfo", m => m.IsStatic && m.GetParameters().Length == 1),
                null, assemblySymbol);

        public static string GetAssemblyDisplay(Compilation compilation, IAssemblySymbol assemblySymbol)
            => RoslynReflection.Invoke<string>(
                RoslynReflection.GetMethod(s_type, "GetAssemblyDisplay", m => m.IsStatic && m.GetParameters().Length == 2),
                null, compilation, assemblySymbol);

        public static Task<Location> GetLocationInGeneratedSourceAsync(
            ISymbol symbol, Document generatedDocument, CancellationToken cancellationToken)
        {
            var symbolKeyType = RoslynReflection.GetType(RoslynReflection.WorkspacesAssembly, "Microsoft.CodeAnalysis.SymbolKey");
            var create = RoslynReflection.GetMethod(symbolKeyType, "Create",
                m => m.IsStatic && m.GetParameters().Length == 2);
            var symbolKey = RoslynReflection.Invoke(create, null, symbol, cancellationToken);
            var method = RoslynReflection.GetMethod(s_type, "GetLocationInGeneratedSourceAsync",
                m => m.IsStatic && m.GetParameters().Length == 3);
            return RoslynReflection.Invoke<Task<Location>>(method, null, symbolKey, generatedDocument, cancellationToken);
        }
    }

    public static class RoslynMetadataAsSourceService
    {
        public static Task<Document> AddSourceToAsync(
            Document document, Compilation symbolCompilation, ISymbol symbol,
            OmniSharpSyntaxFormattingOptionsWrapper formattingOptions, CancellationToken cancellationToken)
        {
            var service = RoslynReflection.GetRequiredLanguageService(
                document, RoslynReflection.FeaturesAssembly, "Microsoft.CodeAnalysis.MetadataAsSource.IMetadataAsSourceService");
            var method = RoslynReflection.GetMethod(service.GetType(), "AddSourceToAsync",
                m => !m.IsStatic && m.GetParameters().Length == 5);
            return RoslynReflection.Invoke<Task<Document>>(
                method, service, document, symbolCompilation, symbol, formattingOptions.UnderlyingObject, cancellationToken);
        }
    }
}
