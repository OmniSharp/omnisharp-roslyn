using System;
using System.Collections.Concurrent;
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
        protected ConcurrentDictionary<string, Document> _cache = new ConcurrentDictionary<string, Document>();

        protected const string SymbolKey = "Microsoft.CodeAnalysis.SymbolKey";
        protected const string MetadataAsSourceHelpers = "Microsoft.CodeAnalysis.MetadataAsSource.MetadataAsSourceHelpers";
        protected const string GetLocationInGeneratedSourceAsync = "GetLocationInGeneratedSourceAsync";
        protected const string AddSourceToAsync = "AddSourceToAsync";
        protected const string Create = "Create";

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

        public async Task<Location> GetExternalSymbolLocation(ISymbol symbol, Document metadataDocument, CancellationToken cancellationToken = new CancellationToken())
        {
            var symbolKeyCreateMethod = _symbolKey.GetMethod(Create, BindingFlags.Static | BindingFlags.NonPublic);
            var symboldId = symbolKeyCreateMethod.InvokeStatic(new object[] { symbol, cancellationToken });

            return await _getLocationInGeneratedSourceAsync.InvokeStatic<Task<Location>>(new object[] { symboldId, metadataDocument, cancellationToken });
        }
    }
}
