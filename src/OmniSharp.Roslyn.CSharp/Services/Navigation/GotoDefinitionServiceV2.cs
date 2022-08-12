#nullable enable

using Microsoft.CodeAnalysis;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.Metadata;
using OmniSharp.Models.v1.SourceGeneratedFile;
using OmniSharp.Models.V2.GotoDefinition;
using OmniSharp.Options;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.GotoDefinition, LanguageNames.CSharp)]
    public class GotoDefinitionServiceV2 : IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>
    {
        private readonly OmniSharpOptions _omnisharpOptions;
        private readonly OmniSharpWorkspace _workspace;
        private readonly ExternalSourceServiceFactory _externalSourceServiceFactory;

        [ImportingConstructor]
        public GotoDefinitionServiceV2(OmniSharpWorkspace workspace, ExternalSourceServiceFactory externalSourceServiceFactory, OmniSharpOptions omnisharpOptions)
        {
            _workspace = workspace;
            _externalSourceServiceFactory = externalSourceServiceFactory;
            _omnisharpOptions = omnisharpOptions;
        }

        public async Task<GotoDefinitionResponse> Handle(GotoDefinitionRequest request)
        {
            var cancellationToken = _externalSourceServiceFactory.CreateCancellationToken(_omnisharpOptions, request.Timeout);
            var externalSourceService = _externalSourceServiceFactory.Create(_omnisharpOptions);
            var document = externalSourceService.FindDocumentInCache(request.FileName) ??
                _workspace.GetDocument(request.FileName);

            if (document == null)
            {
                return new GotoDefinitionResponse();
            }

            var symbol = await GoToDefinitionHelpers.GetDefinitionSymbol(document, request.Line, request.Column, cancellationToken);
            if (symbol?.Locations.IsDefaultOrEmpty != false)
            {
                return new GotoDefinitionResponse();
            }

            if (symbol.Locations[0].IsInSource)
            {
                var definitions = symbol.Locations
                    .Select(location =>
                    {
                        MetadataSource? metadataSource = null;
                        SourceGeneratedFileInfo? sourceGeneratedFileInfo = null;

                        if (IsMetaDataSource(location.SourceTree))
                        {
                            metadataSource = new MetadataSource()
                            {
                                AssemblyName = symbol.ContainingAssembly.Name,
                                ProjectName = document.Project.Name,
                                TypeName = symbol.GetSymbolName()
                            };
                        }
                        else
                        {
                            sourceGeneratedFileInfo = SolutionExtensions.GetSourceGeneratedFileInfo(document.Project.Solution, location);
                        }

                        return new Definition
                        {
                            Location = location.GetMappedLineSpan().GetLocationFromFileLinePositionSpan(),
                            MetadataSource = metadataSource,
                            SourceGeneratedFileInfo = sourceGeneratedFileInfo
                        };
                    })
                    .ToList();

                return new GotoDefinitionResponse()
                {
                    Definitions = definitions
                };
            }
            else
            {
                var maybeSpan = await GoToDefinitionHelpers.GetMetadataMappedSpan(document, symbol, externalSourceService, cancellationToken);

                if (maybeSpan is FileLinePositionSpan lineSpan)
                {
                    return new GotoDefinitionResponse
                    {
                        Definitions = new()
                        {
                            new Definition
                            {
                                Location = lineSpan.GetLocationFromFileLinePositionSpan(),
                                MetadataSource = new OmniSharp.Models.Metadata.MetadataSource()
                                {
                                    AssemblyName = symbol.ContainingAssembly.Name,
                                    ProjectName = document.Project.Name,
                                    TypeName = symbol.GetSymbolName()
                                }
                            }
                        }
                    };
                }

                return new GotoDefinitionResponse();
            }

            static bool IsMetaDataSource(SyntaxTree? syntaxTree)
            {
                return syntaxTree?.FilePath.StartsWith("$metadata$\\Project\\") == true;
            }
        }
    }
}
