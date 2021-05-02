using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using OmniSharp.Mef;
using OmniSharp.Models.Metadata;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Decompilation;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.Metadata, LanguageNames.CSharp)]
    public class MetadataService : IRequestHandler<MetadataRequest, MetadataResponse>
    {
        private readonly OmniSharpOptions _omniSharpOptions;
        private readonly OmniSharpWorkspace _workspace;
        private readonly ExternalSourceServiceFactory _externalSourceServiceFactory;

        [ImportingConstructor]
        public MetadataService(OmniSharpWorkspace workspace, ExternalSourceServiceFactory externalSourceServiceFactory, OmniSharpOptions omniSharpOptions)
        {
            _workspace = workspace;
            _externalSourceServiceFactory = externalSourceServiceFactory;
            _omniSharpOptions = omniSharpOptions;
        }

        public async Task<MetadataResponse> Handle(MetadataRequest request)
        {
            var externalSourceService = _externalSourceServiceFactory.Create(_omniSharpOptions);

            var response = new MetadataResponse();
            foreach (var project in _workspace.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                var symbol = compilation.GetTypeByMetadataName(request.TypeName);
                if (symbol != null && symbol.ContainingAssembly.Name == request.AssemblyName)
                {
                    var cancellationToken = _externalSourceServiceFactory.CreateCancellationToken(_omniSharpOptions, request.Timeout);
                    var (metadataDocument, documentPath) = await externalSourceService.GetAndAddExternalSymbolDocument(project, symbol, cancellationToken);

                    if (metadataDocument != null)
                    {
                        var source = await metadataDocument.GetTextAsync();
                        response.Source = source.ToString();
                        response.SourceName = documentPath;

                        return response;
                    }
                }
            }
            return response;
        }
    }
}
