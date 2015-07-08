using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Roslyn
{
    public class MetadataHelper
    {
#if DNX451
        public static Task<Document> GetDocumentFromMetadata(Document document, ISymbol symbol, CancellationToken cancellationToken = new CancellationToken())
        {
            var topLevelSymbol = GetTopLevelContainingNamedType(symbol);
            var temporaryDocument = document.Project.AddDocument($"#/metadata/{topLevelSymbol.ToDisplayString()}", string.Empty);

            object service = Activator.CreateInstance(_CSharpMetadataAsSourceService.Value, new object[] { temporaryDocument.Project.LanguageServices });
            var method = _CSharpMetadataAsSourceService.Value.GetMethod("AddSourceToAsync");

            return (Task<Document>)method.Invoke(service, new object[] { temporaryDocument, topLevelSymbol, cancellationToken });
        }

        public static async Task<Location> GetSymbolLocationFromMetadata(ISymbol symbol, Document metadataDocument, CancellationToken cancellationToken = new CancellationToken())
        {
            var metadataSemanticModel = await metadataDocument.GetSemanticModelAsync();
            var symbolKeyCreateMethod = _SymbolKey.Value.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic);
            var symboldId = symbolKeyCreateMethod.Invoke(null, new object[] { symbol, metadataSemanticModel.Compilation, cancellationToken });

            return await (Task<Location>)_GetLocationInGeneratedSourceAsync.Value.Invoke(null, new object[] { symboldId, metadataDocument, cancellationToken });
        }

        private static INamedTypeSymbol GetTopLevelContainingNamedType(ISymbol symbol)
        {
            // Traverse up until we find a named type that is parented by the namespace
            var topLevelNamedType = symbol;
            while (topLevelNamedType.ContainingSymbol != symbol.ContainingNamespace ||
                topLevelNamedType.Kind != SymbolKind.NamedType)
            {
                topLevelNamedType = topLevelNamedType.ContainingSymbol;
            }

            return (INamedTypeSymbol)topLevelNamedType;
        }

        private static Lazy<Assembly> featuresAssembly = new Lazy<Assembly>(() => Assembly.Load("Microsoft.CodeAnalysis.Features, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"));
        private static Lazy<Assembly> csharpFeaturesAssembly = new Lazy<Assembly>(() => Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"));
        private static Lazy<Assembly> workspacesAssembly = new Lazy<Assembly>(() => Assembly.Load("Microsoft.CodeAnalysis.Workspaces, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"));

        private static Lazy<Type> _CSharpMetadataAsSourceService = new Lazy<Type>(() =>
        {
            return csharpFeaturesAssembly.Value.GetType("Microsoft.CodeAnalysis.CSharp.MetadataAsSource.CSharpMetadataAsSourceService");
        });

        private static Lazy<Type> _SymbolKey = new Lazy<Type>(() =>
        {
            return workspacesAssembly.Value.GetType("Microsoft.CodeAnalysis.SymbolKey");
        });

        private static Lazy<Type> _MetadataAsSourceHelpers = new Lazy<Type>(() =>
        {
            return featuresAssembly.Value.GetType("Microsoft.CodeAnalysis.MetadataAsSource.MetadataAsSourceHelpers", true);
        });

        private static Lazy<MethodInfo> _GetLocationInGeneratedSourceAsync = new Lazy<MethodInfo>(() =>
        {
            return _MetadataAsSourceHelpers.Value.GetMethod("GetLocationInGeneratedSourceAsync");
        });
#endif
    }
}
