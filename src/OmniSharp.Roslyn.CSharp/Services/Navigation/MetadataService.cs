using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmnisharpEndpoints.Metadata, LanguageNames.CSharp)]
    public class MetadataService : RequestHandler<MetadataRequest, MetadataResponse>
    {
        private readonly MetadataHelper _metadataHelper;
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public MetadataService(OmnisharpWorkspace workspace, MetadataHelper metadataHelper)
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
                    var document = await _metadataHelper.GetDocumentFromMetadata(project, symbol, cancellationSource.Token);
                    if (document != null)
                    {
                        var source = await document.GetTextAsync();
                        response.SourceName = _metadataHelper.GetFilePathForSymbol(project, symbol);
                        response.Source = source.ToString();

                        return response;
                    }
                }
            }
            return response;
        }
    }
}
