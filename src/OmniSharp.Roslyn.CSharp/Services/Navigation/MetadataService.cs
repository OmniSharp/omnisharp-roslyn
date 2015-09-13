using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using OmniSharp.Models;
using OmniSharp.Roslyn;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("metadata")]
        public async Task<MetadataResponse> Metadata(MetadataRequest request)
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
