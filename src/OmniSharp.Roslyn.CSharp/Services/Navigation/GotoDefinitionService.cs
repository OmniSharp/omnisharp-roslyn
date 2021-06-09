#nullable enable

using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.GotoDefinition;
using OmniSharp.Models.Metadata;
using OmniSharp.Options;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.GotoDefinition, LanguageNames.CSharp)]
    public class GotoDefinitionService : IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>
    {
        private readonly OmniSharpOptions _omnisharpOptions;
        private readonly OmniSharpWorkspace _workspace;
        private readonly ExternalSourceServiceFactory _externalSourceServiceFactory;

        [ImportingConstructor]
        public GotoDefinitionService(OmniSharpWorkspace workspace, ExternalSourceServiceFactory externalSourceServiceFactory, OmniSharpOptions omnisharpOptions)
        {
            _workspace = workspace;
            _externalSourceServiceFactory = externalSourceServiceFactory;
            _omnisharpOptions = omnisharpOptions;
        }

        public async Task<GotoDefinitionResponse> Handle(GotoDefinitionRequest request)
        {
            var externalSourceService = _externalSourceServiceFactory.Create(_omnisharpOptions);
            var cancellationToken = _externalSourceServiceFactory.CreateCancellationToken(_omnisharpOptions, request.Timeout);
            var document = externalSourceService.FindDocumentInCache(request.FileName) ??
                _workspace.GetDocument(request.FileName);

            var symbol = await GoToDefinitionHelpers.GetDefinitionSymbol(document, request.Line, request.Column, cancellationToken);
            if (symbol == null)
            {
                return new GotoDefinitionResponse();
            }

            var location = symbol.Locations.First();

            GotoDefinitionResponse? response = null;
            if (location.IsInSource)
            {
                var lineSpan = symbol.Locations.First().GetMappedLineSpan();
                response = new GotoDefinitionResponse
                {
                    FileName = lineSpan.Path,
                    Line = lineSpan.StartLinePosition.Line,
                    Column = lineSpan.StartLinePosition.Character,
                    SourceGeneratedInfo = GoToDefinitionHelpers.GetSourceGeneratedFileInfo(_workspace, location)
                };
            }
            else if (location.IsInMetadata && request.WantMetadata)
            {
                var maybeSpan = await GoToDefinitionHelpers.GetMetadataMappedSpan(document, symbol, externalSourceService, cancellationToken);

                if (maybeSpan is FileLinePositionSpan lineSpan)
                {
                    response = new GotoDefinitionResponse
                    {
                        Line = lineSpan.StartLinePosition.Line,
                        Column = lineSpan.StartLinePosition.Character,
                        MetadataSource = new MetadataSource()
                        {
                            AssemblyName = symbol.ContainingAssembly.Name,
                            ProjectName = document.Project.Name,
                            TypeName = symbol.GetSymbolName()
                        },
                    };
                }
            }

            return response ?? new GotoDefinitionResponse();
        }
    }
}
