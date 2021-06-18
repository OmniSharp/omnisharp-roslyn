using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.MetadataAsSource;
using OmniSharp.Extensions;

namespace OmniSharp.Roslyn
{
    [Export(typeof(MetadataExternalSourceService)), Shared]
    public class MetadataExternalSourceService : BaseExternalSourceService, IExternalSourceService
    {
        private const string MetadataKey = "$Metadata$";

        [ImportingConstructor]
        public MetadataExternalSourceService() : base()
        {
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

                document = await OmniSharpMetadataAsSourceService.AddSourceToAsync(
                    temporaryDocument,
                    await metadataProject.GetCompilationAsync(),
                    topLevelSymbol,
                    cancellationToken);

                _cache.TryAdd(fileName, document);
            }

            return (document, fileName);
        }
    }
}
