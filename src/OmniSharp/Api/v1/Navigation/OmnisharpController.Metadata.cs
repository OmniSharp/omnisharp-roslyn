using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Roslyn;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
#if DNX451
        [HttpPost("metadata")]
        public async Task<MetadataResponse> Metadata(MetadataRequest request)
        {
            var response = new MetadataResponse();
            foreach (var project in _workspace.CurrentSolution.Projects)
            {
                var compliation = await project.GetCompilationAsync();
                var symbol = compliation.GetTypeByMetadataName(request.TypeName);
                if (symbol != null && symbol.ContainingAssembly.Name == request.AssemblyName)
                {
                    var document = await MetadataHelper.GetDocumentFromMetadata(project, symbol);
                    var source = await document.GetTextAsync();
                    response.SourceName = MetadataHelper.GetFilePathForSymbol(project, symbol);
                    response.Source = source.ToString();

                    return response;
                }
            }
            return response;
        }
#else
        [HttpPost("metadata")]
        public Task<MetadataResponse> Metadata(MetadataRequest request)
        {
            // this handles the case where the method isn't async on coreclr
            return Task.FromResult(new MetadataResponse());
        }
#endif
    }
}
