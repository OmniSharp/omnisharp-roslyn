using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models.Metadata;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.Metadata, LanguageNames.CSharp)]
    public class MetadataService : IRequestHandler<MetadataRequest, MetadataResponse>
    {
        private readonly MetadataHelper _metadataHelper;
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public MetadataService(OmniSharpWorkspace workspace, MetadataHelper metadataHelper)
        {
            _workspace = workspace;
            _metadataHelper = metadataHelper;
        }

        public async Task<MetadataResponse> Handle(MetadataRequest request)
        {
            var response = new MetadataResponse();
            foreach (var project in _workspace.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                var symbol = compilation.GetTypeByMetadataName(request.TypeName);
                if (symbol != null && symbol.ContainingAssembly.Name == request.AssemblyName)
                {
                    var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(request.Timeout));
                    var metadataDocumentResult = await _metadataHelper.GetAndAddDocumentFromMetadata(project, symbol, cancellationSource.Token);
                    if (metadataDocumentResult.Document != null)
                    {
                        var source = await metadataDocumentResult.Document.GetTextAsync();
                        response.Source = source.ToString();
                        response.SourceName = metadataDocumentResult.DocumentPath;

                        return response;
                    }
                }
            }
            return response;
        }
    }
}
