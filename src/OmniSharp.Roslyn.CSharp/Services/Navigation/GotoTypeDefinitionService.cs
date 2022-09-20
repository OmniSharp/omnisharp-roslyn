#nullable enable

using Microsoft.CodeAnalysis;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.GotoTypeDefinition;
using OmniSharp.Options;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.GotoTypeDefinition, LanguageNames.CSharp)]
    public class GotoTypeDefinitionService : IRequestHandler<GotoTypeDefinitionRequest, GotoTypeDefinitionResponse>
    {
        private readonly OmniSharpOptions _omnisharpOptions;
        private readonly OmniSharpWorkspace _workspace;
        private readonly ExternalSourceServiceFactory _externalSourceServiceFactory;

        [ImportingConstructor]
        public GotoTypeDefinitionService(OmniSharpWorkspace workspace, ExternalSourceServiceFactory externalSourceServiceFactory, OmniSharpOptions omnisharpOptions)
        {
            _workspace = workspace;
            _externalSourceServiceFactory = externalSourceServiceFactory;
            _omnisharpOptions = omnisharpOptions;
        }

        public async Task<GotoTypeDefinitionResponse> Handle(GotoTypeDefinitionRequest request)
        {
            var cancellationToken = _externalSourceServiceFactory.CreateCancellationToken(_omnisharpOptions, request.Timeout);
            var externalSourceService = _externalSourceServiceFactory.Create(_omnisharpOptions);
            var document = externalSourceService.FindDocumentInCache(request.FileName) ??
                _workspace.GetDocument(request.FileName);

            if (document == null)
            {
                return new GotoTypeDefinitionResponse();
            }

            var typeSymbol = await GotoTypeDefinitionHelpers.GetTypeOfSymbol(document, request.Line, request.Column, cancellationToken);
            if (typeSymbol?.Locations.IsDefaultOrEmpty != false)
            {
                return new GotoTypeDefinitionResponse();
            }

            if (typeSymbol.Locations[0].IsInSource)
            {
                return new GotoTypeDefinitionResponse()
                {
                    Definitions = typeSymbol.Locations
                        .Select(location => new TypeDefinition
                        {
                            Location = location.GetMappedLineSpan().GetLocationFromFileLinePositionSpan(),
                            SourceGeneratedFileInfo = SolutionExtensions.GetSourceGeneratedFileInfo(document.Project.Solution, location)
                        })
                        .ToList()
                };
            }
            else
            {
                var maybeSpan = await GoToDefinitionHelpers.GetMetadataMappedSpan(document, typeSymbol, externalSourceService, cancellationToken);

                if (maybeSpan is FileLinePositionSpan lineSpan)
                {
                    return new GotoTypeDefinitionResponse
                    {
                        Definitions = new()
                        {
                            new TypeDefinition
                            {
                                Location = lineSpan.GetLocationFromFileLinePositionSpan(),
                                MetadataSource = new OmniSharp.Models.Metadata.MetadataSource()
                                {
                                    AssemblyName = typeSymbol.ContainingAssembly.Name,
                                    ProjectName = document.Project.Name,
                                    TypeName = typeSymbol.GetSymbolName()
                                }
                            }
                        }
                    };
                }

                return new GotoTypeDefinitionResponse();
            }
        }
    }
}
