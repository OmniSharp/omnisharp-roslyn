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
        [HttpPost("metadata")]
        public async Task<MetadataResponse> Metadata(MetadataRequest request)
        {
            var response = new MetadataResponse();
#if DNX451
            foreach (var project in _workspace.CurrentSolution.Projects)
            {
                var compliation = await project.GetCompilationAsync();
                var symbol = compliation.GetTypeByMetadataName(request.TypeName);
                if (symbol != null)
                {
                    // AssemblyName is optional.
                    if (string.IsNullOrEmpty(request.AssemblyName) || symbol.ContainingAssembly.Name == request.AssemblyName)
                    {
                        var document = await MetadataHelper.GetDocumentFromMetadata(project, symbol);
                        var source = await document.GetTextAsync();
                        response.Source = source.ToString();
                        return response;
                    }
                }
            }
#endif
            return response;
        }
    }
}
