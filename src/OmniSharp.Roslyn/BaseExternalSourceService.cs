using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Roslyn
{
    public abstract class BaseExternalSourceService
    {
        protected readonly IAssemblyLoader _loader;
        protected readonly Lazy<Assembly> _featureAssembly;
        protected readonly Lazy<Assembly> _csharpFeatureAssembly;
        protected readonly Lazy<Assembly> _workspaceAssembly;
        protected readonly Lazy<Type> _symbolKey;
        protected readonly Lazy<Type> _metadataAsSourceHelper;
        protected readonly Lazy<MethodInfo> _getLocationInGeneratedSourceAsync;
        protected Dictionary<string, Document> _cache = new Dictionary<string, Document>();

        protected const string SymbolKey = "Microsoft.CodeAnalysis.SymbolKey";
        protected const string MetadataAsSourceHelpers = "Microsoft.CodeAnalysis.MetadataAsSource.MetadataAsSourceHelpers";
        protected const string GetLocationInGeneratedSourceAsync = "GetLocationInGeneratedSourceAsync";
        protected const string AddSourceToAsync = "AddSourceToAsync";
        protected const string Create = "Create";
        protected const string MetadataKey = "$Metadata$";

        protected BaseExternalSourceService(IAssemblyLoader loader)
        {
            _loader = loader;
            _featureAssembly = _loader.LazyLoad(Configuration.RoslynFeatures);
            _csharpFeatureAssembly = _loader.LazyLoad(Configuration.RoslynCSharpFeatures);
            _workspaceAssembly = _loader.LazyLoad(Configuration.RoslynWorkspaces);

            _symbolKey = _workspaceAssembly.LazyGetType(SymbolKey);
            _metadataAsSourceHelper = _featureAssembly.LazyGetType(MetadataAsSourceHelpers);
            _getLocationInGeneratedSourceAsync = _metadataAsSourceHelper.LazyGetMethod(GetLocationInGeneratedSourceAsync);
        }

        public Document FindDocumentInCache(string fileName)
        {
            if (_cache.TryGetValue(fileName, out var metadataDocument))
            {
                return metadataDocument;
            }

            return null;
        }

        public string GetSymbolName(ISymbol symbol)
        {
            var topLevelSymbol = symbol.GetTopLevelContainingNamedType();
            return GetTypeDisplayString(topLevelSymbol);
        }


        public async Task<Location> GetExternalSymbolLocation(ISymbol symbol, Document metadataDocument, CancellationToken cancellationToken = new CancellationToken())
        {
            var symbolKeyCreateMethod = _symbolKey.GetMethod(Create, BindingFlags.Static | BindingFlags.NonPublic);
            var symboldId = symbolKeyCreateMethod.InvokeStatic(new object[] { symbol, cancellationToken });

            return await _getLocationInGeneratedSourceAsync.InvokeStatic<Task<Location>>(new object[] { symboldId, metadataDocument, cancellationToken });
        }

        protected static string GetTypeDisplayString(INamedTypeSymbol symbol)
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

        protected static string GetFilePathForSymbol(Project project, ISymbol symbol)
        {
            var topLevelSymbol = symbol.GetTopLevelContainingNamedType();
            return $"$metadata$/Project/{Folderize(project.Name)}/Assembly/{Folderize(topLevelSymbol.ContainingAssembly.Name)}/Symbol/{Folderize(GetTypeDisplayString(topLevelSymbol))}.cs".Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        protected static string Folderize(string path) => string.Join("/", path.Split('.'));
    }
}
