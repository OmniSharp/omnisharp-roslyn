using System;
using System.Collections.Generic;
using System.Composition;
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
    [Export(typeof(MetadataExternalSourceService)), Shared]
    public class MetadataExternalSourceService : BaseExternalSourceService, IExternalSourceService
    {
        private const string MetadataKey = "$Metadata$";
        private readonly Lazy<Type> _csharpMetadataAsSourceService;
        private const string CSharpMetadataAsSourceService = "Microsoft.CodeAnalysis.CSharp.MetadataAsSource.CSharpMetadataAsSourceService";

        [ImportingConstructor]
        public MetadataExternalSourceService(IAssemblyLoader loader) : base(loader)
        {
            _csharpMetadataAsSourceService = _csharpFeatureAssembly.LazyGetType(CSharpMetadataAsSourceService);
        }

        public async Task<(Document document, string documentPath)> GetAndAddExternalSymbolDocument(Project project, ISymbol symbol, CancellationToken cancellationToken)
        {
            var fileName = symbol.GetFilePathForExternalSymbol(project);

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

            if (!_cache.TryGetValue(fileName, out var document))
            {
                var topLevelSymbol = symbol.GetTopLevelContainingNamedType();

                var temporaryDocument = metadataProject.AddDocument(fileName, string.Empty);
                var service = _csharpMetadataAsSourceService.CreateInstance();
                var method = _csharpMetadataAsSourceService.GetMethod(AddSourceToAsync);

                var documentTask = method.Invoke<Task<Document>>(service, new object[] { temporaryDocument, await metadataProject.GetCompilationAsync(), topLevelSymbol, cancellationToken });
                document = await documentTask;

                _cache.TryAdd(fileName, document);
            }

            return (document, fileName);
        }
    }
}
