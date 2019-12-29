using Microsoft.CodeAnalysis;
using OmniSharp.Extensions;
using OmniSharp.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp.Roslyn.CSharp.Services.Decompilation
{
    public class DecompilationHelper
    {
        //private const string CSharpDecompiledSourceService = "Microsoft.CodeAnalysis.CSharp.DecompiledSource.CSharpDecompiledSourceService";
        private const string AddSourceToAsync = "AddSourceToAsync";
        private const string DecompiledKey = "$Decompiled$";

        private readonly IAssemblyLoader _loader;
        private readonly Lazy<Assembly> _csharpFeatureAssembly;
        //private readonly Lazy<Type> _csharpDecompiledSourceService;
        private Dictionary<string, Document> _decompiledDocumentCache = new Dictionary<string, Document>();

        public DecompilationHelper(IAssemblyLoader loader)
        {
            _loader = loader;
            _csharpFeatureAssembly = _loader.LazyLoad(Configuration.RoslynCSharpFeatures);
            // _csharpDecompiledSourceService = _csharpFeatureAssembly.LazyGetType(CSharpDecompiledSourceService);
        }

        public async Task<(Document metadataDocument, string documentPath)> GetAndAddDecompiledDocument(Project project, ISymbol symbol, CancellationToken cancellationToken = new CancellationToken())
        {
            var fileName = GetFilePathForSymbol(project, symbol);

            Project decompilationProject;

            // since submission projects cannot have new documents added to it
            // we will use a separate project to hold decompiled documents
            if (project.IsSubmission)
            {
                decompilationProject = project.Solution.Projects.FirstOrDefault(x => x.Name == DecompiledKey);
                if (decompilationProject == null)
                {
                    decompilationProject = project.Solution.AddProject(DecompiledKey, $"{DecompiledKey}.dll", LanguageNames.CSharp)
                        .WithCompilationOptions(project.CompilationOptions)
                        .WithMetadataReferences(project.MetadataReferences);
                }
            }
            else
            {
                // for regular projects we will use current project to store decompiled docs
                decompilationProject = project;
            }

            if (!_decompiledDocumentCache.TryGetValue(fileName, out var metadataDocument))
            {
                var topLevelSymbol = symbol.GetTopLevelContainingNamedType();

                var temporaryDocument = decompilationProject.AddDocument(fileName, string.Empty);
                var service = new OmniSharpCSharpDecompiledSourceService(temporaryDocument.Project.LanguageServices);
                var documentTask = service.AddSourceToAsync(temporaryDocument, await decompilationProject.GetCompilationAsync(), topLevelSymbol, cancellationToken);
                //var method = _csharpDecompiledSourceService.GetMethod(AddSourceToAsync);

                //var documentTask = method.Invoke<Task<Document>>(service, new object[] { temporaryDocument, await decompilationProject.GetCompilationAsync(), topLevelSymbol, default(CancellationToken) });
                metadataDocument = await documentTask;

                _decompiledDocumentCache[fileName] = metadataDocument;
            }

            return (metadataDocument, fileName);
        }

        private static string GetFilePathForSymbol(Project project, ISymbol symbol)
        {
            var topLevelSymbol = symbol.GetTopLevelContainingNamedType();
            return $"$decompiled$/Project/{Folderize(project.Name)}/Assembly/{Folderize(topLevelSymbol.ContainingAssembly.Name)}/Symbol/{Folderize(GetTypeDisplayString(topLevelSymbol))}.cs".Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
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

        private static string Folderize(string path) => string.Join("/", path.Split('.'));
    }
}
