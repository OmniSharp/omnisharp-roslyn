using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace OmniSharp.Roslyn
{
    public class MetadataHelper
    {
        private static Lazy<Type> _csharpMetadataAsSourceService = new Lazy<Type>(() =>
        {
            var assembly = Assembly.Load(new AssemblyName("Microsoft.CodeAnalysis.CSharp.Features, Version=1.1.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"));
            return assembly.GetType("Microsoft.CodeAnalysis.CSharp.MetadataAsSource.CSharpMetadataAsSourceService");
        });

        private static Lazy<Type> _symbolKey = new Lazy<Type>(() =>
        {
            var assembly = Assembly.Load(new AssemblyName("Microsoft.CodeAnalysis.Workspaces, Version=1.1.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"));
            return assembly.GetType("Microsoft.CodeAnalysis.SymbolKey");
        });

        private static Lazy<MethodInfo> _getLocationInGeneratedSourceAsync = new Lazy<MethodInfo>(() =>
        {
            var assembly = Assembly.Load(new AssemblyName("Microsoft.CodeAnalysis.Features, Version=1.1.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"));
            var type = assembly.GetType("Microsoft.CodeAnalysis.MetadataAsSource.MetadataAsSourceHelpers", throwOnError: true, ignoreCase: true);
            return type.GetMethod("GetLocationInGeneratedSourceAsync");
        });

        public static string GetSymbolName(ISymbol symbol)
        {
            var topLevelSymbol = GetTopLevelContainingNamedType(symbol);
            return GetTypeDisplayString(topLevelSymbol);
        }

        public static string GetFilePathForSymbol(Project project, ISymbol symbol)
        {
            var topLevelSymbol = GetTopLevelContainingNamedType(symbol);
            return $"metadata/Project/{Folderize(project.Name)}/Assembly/{Folderize(topLevelSymbol.ContainingAssembly.Name)}/Symbol/{Folderize(GetTypeDisplayString(topLevelSymbol))}.cs".Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        public static Task<Document> GetDocumentFromMetadata(Project project, ISymbol symbol, CancellationToken cancellationToken = new CancellationToken())
        {
            var filePath = GetFilePathForSymbol(project, symbol);
            var topLevelSymbol = GetTopLevelContainingNamedType(symbol);
            var temporaryDocument = project.AddDocument(filePath, string.Empty);

            var service = Activator.CreateInstance(_csharpMetadataAsSourceService.Value, new object[] { temporaryDocument.Project.LanguageServices });
            var method = _csharpMetadataAsSourceService.Value.GetMethod("AddSourceToAsync");

            return (Task<Document>)method.Invoke(service, new object[] { temporaryDocument, topLevelSymbol, cancellationToken });
        }

        public static async Task<Location> GetSymbolLocationFromMetadata(ISymbol symbol, Document metadataDocument, CancellationToken cancellationToken = new CancellationToken())
        {
            var metadataSemanticModel = await metadataDocument.GetSemanticModelAsync();
            var symbolKeyCreateMethod = _symbolKey.Value.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic);
            var symboldId = symbolKeyCreateMethod.Invoke(null, new object[] { symbol, metadataSemanticModel.Compilation, cancellationToken });

            return await (Task<Location>)_getLocationInGeneratedSourceAsync.Value.Invoke(null, new object[] { symboldId, metadataDocument, cancellationToken });
        }

        private static string GetTypeDisplayString(INamedTypeSymbol symbol)
        {
            if (symbol.SpecialType != SpecialType.None)
            {
                var specialType = symbol.SpecialType;
                var name = Enum.GetName(typeof(SpecialType), symbol.SpecialType).Replace("_", ".");
                return name;
            }

            if (symbol.IsGenericType)
            {
                symbol = symbol.ConstructUnboundGenericType();
            }

            if (symbol.IsUnboundGenericType)
            {
                // TODO: Is this the best to get the fully metadata name?
                var parts = symbol.ToDisplayParts();
                var filteredParts = parts.Where(x => x.Kind != SymbolDisplayPartKind.Punctuation).ToArray();
                var typeName = new StringBuilder();
                foreach (var part in filteredParts.Take(filteredParts.Length - 1))
                {
                    typeName.Append(part.Symbol.Name);
                    typeName.Append(".");
                }
                typeName.Append(symbol.MetadataName);

                return typeName.ToString();
            }

            return symbol.ToDisplayString();
        }

        private static string Folderize(string path)
        {
            return string.Join("/", path.Split('.'));
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
    }
}
