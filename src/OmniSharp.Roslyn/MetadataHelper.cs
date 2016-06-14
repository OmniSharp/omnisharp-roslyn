using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Services;

namespace OmniSharp.Roslyn
{
    public class MetadataHelper
    {
        private readonly IOmnisharpAssemblyLoader _loader;
        private readonly Lazy<Assembly> _featureAssembly;
        private readonly Lazy<Assembly> _csharpFeatureAssembly;
        private readonly Lazy<Assembly> _workspaceAssembly;
        private readonly Lazy<Type> _csharpMetadataAsSourceServices;
        private readonly Lazy<Type> _symbolKey;
        private readonly Lazy<Type> _metadataAsSourceHelper;
        private readonly Lazy<MethodInfo> _getLocationInGeneratedSourceAsync;

        public MetadataHelper(IOmnisharpAssemblyLoader loader)
        {
            _loader = loader;
            _featureAssembly = _loader.LazyLoad(Configuration.GetRoslynAssemblyFullName("Microsoft.CodeAnalysis.Features"));
            _csharpFeatureAssembly = _loader.LazyLoad(Configuration.GetRoslynAssemblyFullName("Microsoft.CodeAnalysis.CSharp.Features"));
            _workspaceAssembly = _loader.LazyLoad(Configuration.GetRoslynAssemblyFullName("Microsoft.CodeAnalysis.Workspaces"));

            _csharpMetadataAsSourceServices = new Lazy<Type>(() =>
            {
                return _csharpFeatureAssembly.Value.GetType("Microsoft.CodeAnalysis.CSharp.MetadataAsSource.CSharpMetadataAsSourceService");
            });

            _symbolKey = new Lazy<Type>(() =>
            {
                return _workspaceAssembly.Value.GetType("Microsoft.CodeAnalysis.SymbolKey");
            });

            _metadataAsSourceHelper = new Lazy<Type>(() =>
            {
                var type = _featureAssembly.Value.GetType("Microsoft.CodeAnalysis.MetadataAsSource.MetadataAsSourceHelpers");
                if (type == null)
                {
                    throw new IndexOutOfRangeException($"Could not find type Microsoft.CodeAnalysis.MetadataAsSource.MetadataAsSourceHelpers");
                }
                return type;
            });

            _getLocationInGeneratedSourceAsync = new Lazy<MethodInfo>(() =>
            {
                return _metadataAsSourceHelper.Value.GetMethod("GetLocationInGeneratedSourceAsync");
            });
        }

        public string GetSymbolName(ISymbol symbol)
        {
            var topLevelSymbol = GetTopLevelContainingNamedType(symbol);
            return GetTypeDisplayString(topLevelSymbol);
        }

        public string GetFilePathForSymbol(Project project, ISymbol symbol)
        {
            var topLevelSymbol = GetTopLevelContainingNamedType(symbol);
            return $"metadata/Project/{Folderize(project.Name)}/Assembly/{Folderize(topLevelSymbol.ContainingAssembly.Name)}/Symbol/{Folderize(GetTypeDisplayString(topLevelSymbol))}.cs".Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        public Task<Document> GetDocumentFromMetadata(Project project, ISymbol symbol, CancellationToken cancellationToken = new CancellationToken())
        {
            var filePath = GetFilePathForSymbol(project, symbol);
            var topLevelSymbol = GetTopLevelContainingNamedType(symbol);
            var temporaryDocument = project.AddDocument(filePath, string.Empty);

            object service = Activator.CreateInstance(_csharpMetadataAsSourceServices.Value, new object[] { temporaryDocument.Project.LanguageServices });
            var method = _csharpMetadataAsSourceServices.Value.GetMethod("AddSourceToAsync");

            return (Task<Document>)method.Invoke(service, new object[] { temporaryDocument, topLevelSymbol, cancellationToken });
        }

        public async Task<Location> GetSymbolLocationFromMetadata(ISymbol symbol, Document metadataDocument, CancellationToken cancellationToken = new CancellationToken())
        {
            var symbolKeyCreateMethod = _symbolKey.Value.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic);
            var symboldId = symbolKeyCreateMethod.Invoke(null, new object[] { symbol, cancellationToken });

            return await (Task<Location>)_getLocationInGeneratedSourceAsync.Value.Invoke(null, new object[] { symboldId, metadataDocument, cancellationToken });
        }

        private string GetTypeDisplayString(INamedTypeSymbol symbol)
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

        private string Folderize(string path)
        {
            return string.Join("/", path.Split('.'));
        }

        private INamedTypeSymbol GetTopLevelContainingNamedType(ISymbol symbol)
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
