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
        private readonly IAssemblyLoader _loader;
        private readonly Lazy<Assembly> _featureAssembly;
        private readonly Lazy<Assembly> _csharpFeatureAssembly;
        private readonly Lazy<Assembly> _workspaceAssembly;
        private readonly Lazy<Type> _csharpMetadataAsSourceService;
        private readonly Lazy<Type> _symbolKey;
        private readonly Lazy<Type> _metadataAsSourceHelper;
        private readonly Lazy<MethodInfo> _getLocationInGeneratedSourceAsync;

        private const string CSharpMetadataAsSourceService = "Microsoft.CodeAnalysis.CSharp.MetadataAsSource.CSharpMetadataAsSourceService";
        private const string SymbolKey = "Microsoft.CodeAnalysis.SymbolKey";
        private const string MetadataAsSourceHelpers = "Microsoft.CodeAnalysis.MetadataAsSource.MetadataAsSourceHelpers";
        private const string GetLocationInGeneratedSourceAsync = "GetLocationInGeneratedSourceAsync";
        private const string AddSourceToAsync = "AddSourceToAsync";
        private const string Create = "Create";

        public MetadataHelper(IAssemblyLoader loader)
        {
            _loader = loader;
            _featureAssembly = _loader.LazyLoad(Configuration.RoslynFeatures);
            _csharpFeatureAssembly = _loader.LazyLoad(Configuration.RoslynCSharpFeatures);
            _workspaceAssembly = _loader.LazyLoad(Configuration.RoslynWorkspaces);

            _csharpMetadataAsSourceService = _csharpFeatureAssembly.LazyGetType(CSharpMetadataAsSourceService);
            _symbolKey = _workspaceAssembly.LazyGetType(SymbolKey);
            _metadataAsSourceHelper = _featureAssembly.LazyGetType(MetadataAsSourceHelpers);

            _getLocationInGeneratedSourceAsync = _metadataAsSourceHelper.LazyGetMethod(GetLocationInGeneratedSourceAsync);
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

            // since submission projects cannot have new documents added to it
            // we will use temporary project to hold metadata documents
            var metadataProject = project.IsSubmission 
                ? project.Solution.AddProject("metadataTemp", "metadataTemp.dll", LanguageNames.CSharp)
                    .WithCompilationOptions(project.CompilationOptions)
                    .WithMetadataReferences(project.MetadataReferences)
                : project;

            var temporaryDocument = metadataProject.AddDocument(filePath, string.Empty);
            var service = _csharpMetadataAsSourceService.CreateInstance(temporaryDocument.Project.LanguageServices);
            var method = _csharpMetadataAsSourceService.GetMethod(AddSourceToAsync);

            return method.Invoke<Task<Document>>(service, new object[] { temporaryDocument, topLevelSymbol, cancellationToken });
        }

        public async Task<Location> GetSymbolLocationFromMetadata(ISymbol symbol, Document metadataDocument, CancellationToken cancellationToken = new CancellationToken())
        {
            var symbolKeyCreateMethod = _symbolKey.GetMethod(Create, BindingFlags.Static | BindingFlags.Public);
            var symboldId = symbolKeyCreateMethod.InvokeStatic(new object[] { symbol, cancellationToken });

            return await _getLocationInGeneratedSourceAsync.InvokeStatic<Task<Location>>(new object[] { symboldId, metadataDocument, cancellationToken });
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
