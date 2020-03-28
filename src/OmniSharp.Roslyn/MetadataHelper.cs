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
    public class MetadataHelper : MetadataHelperBase
    {
        private readonly Lazy<Type> _csharpMetadataAsSourceService;
        private Dictionary<string, Document> _metadataDocumentCache = new Dictionary<string, Document>();
        private const string CSharpMetadataAsSourceService = "Microsoft.CodeAnalysis.CSharp.MetadataAsSource.CSharpMetadataAsSourceService";

        public MetadataHelper(IAssemblyLoader loader) : base(loader)
        {
            _csharpMetadataAsSourceService = _csharpFeatureAssembly.LazyGetType(CSharpMetadataAsSourceService);
        }

        public Document FindDocumentInMetadataCache(string fileName)
        {
            if (_metadataDocumentCache.TryGetValue(fileName, out var metadataDocument))
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

        public async Task<(Document metadataDocument, string documentPath)> GetAndAddDocumentFromMetadata(Project project, ISymbol symbol, CancellationToken cancellationToken = new CancellationToken())
        {
            var fileName = GetFilePathForSymbol(project, symbol);

            Project metadataProject;

            // since submission projects cannot have new documents added to it
            // we will use a separate project to hold metadata documents
            if (project.IsSubmission)
            {
                metadataProject = project.Solution.Projects.FirstOrDefault(x => x.Name == MetadataKey);
                if (metadataProject == null)
                {
                    metadataProject = project.Solution.AddProject(MetadataKey, $"{MetadataKey}.dll", LanguageNames.CSharp)
                        .WithCompilationOptions(project.CompilationOptions)
                        .WithMetadataReferences(project.MetadataReferences);
                }
            }
            else
            {
                // for regular projects we will use current project to store metadata
                metadataProject = project;
            }

            if (!_metadataDocumentCache.TryGetValue(fileName, out var metadataDocument))
            {
                var topLevelSymbol = symbol.GetTopLevelContainingNamedType();

                var temporaryDocument = metadataProject.AddDocument(fileName, string.Empty);
                var service = _csharpMetadataAsSourceService.CreateInstance(temporaryDocument.Project.LanguageServices);
                var method = _csharpMetadataAsSourceService.GetMethod(AddSourceToAsync);

                var documentTask = method.Invoke<Task<Document>>(service, new object[] { temporaryDocument, await metadataProject.GetCompilationAsync(), topLevelSymbol, cancellationToken });
                metadataDocument = await documentTask;

                _metadataDocumentCache[fileName] = metadataDocument;
            }

            return (metadataDocument, fileName);
        }
    }
}
