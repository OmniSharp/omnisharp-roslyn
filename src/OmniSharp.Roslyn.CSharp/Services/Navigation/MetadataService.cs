using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Roslyn;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(typeof(RequestHandler<MetadataRequest, MetadataResponse>), LanguageNames.CSharp)]
    public class MetadataService : RequestHandler<MetadataRequest, MetadataResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public MetadataService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
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
                    var document = await MetadataHelper.GetDocumentFromMetadata(project, symbol, cancellationSource.Token);
                    if (document != null)
                    {
                        var source = await document.GetTextAsync();
                        response.SourceName = MetadataHelper.GetFilePathForSymbol(project, symbol);
                        response.Source = source.ToString();

                        return response;
                    }
                }
            }
            return response;
        }
    }
}
