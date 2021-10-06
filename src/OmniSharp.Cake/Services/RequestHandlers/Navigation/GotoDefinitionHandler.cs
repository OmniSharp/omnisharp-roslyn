using System;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.GotoDefinition;
using OmniSharp.Models.Metadata;
using OmniSharp.Roslyn;
using OmniSharp.Utilities;

namespace OmniSharp.Cake.Services.RequestHandlers.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.GotoDefinition, Constants.LanguageNames.Cake), Shared]
    public class GotoDefinitionHandler : CakeRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>
    {
        private readonly MetadataExternalSourceService _metadataExternalSourceService;

        [ImportingConstructor]
        public GotoDefinitionHandler(
            OmniSharpWorkspace workspace,
            MetadataExternalSourceService metadataExternalSourceService)
            : base(workspace)
        {
            _metadataExternalSourceService = metadataExternalSourceService ?? throw new ArgumentNullException(nameof(metadataExternalSourceService));
        }

        protected override async Task<GotoDefinitionResponse> TranslateResponse(GotoDefinitionResponse response, GotoDefinitionRequest request)
        {
            if (string.IsNullOrEmpty(response.FileName) ||
                !response.FileName.Equals(Constants.Paths.Generated))
            {
                if (PlatformHelper.IsWindows && !string.IsNullOrEmpty(response.FileName))
                {
                    response.FileName = response.FileName.Replace('/', '\\');
                }
                return response;
            }

            if (!request.WantMetadata)
            {
                return new GotoDefinitionResponse();
            }

            var alias = (await GotoDefinitionHandlerHelper.GetAliasFromMetadataAsync(
                Workspace,
                request.FileName,
                response.Line,
                request.Timeout,
                _metadataExternalSourceService
            )).FirstOrDefault();

            if (alias == null)
            {
                return new GotoDefinitionResponse();
            }

            return new GotoDefinitionResponse
            {
                FileName = alias.MetadataDocument.FilePath ?? alias.MetadataDocument.Name,
                Line = alias.LineSpan.StartLinePosition.Line,
                Column = alias.LineSpan.StartLinePosition.Character,
                MetadataSource = new MetadataSource
                {
                    AssemblyName = alias.Symbol.ContainingAssembly.Name,
                    ProjectName = alias.Document.Project.Name,
                    TypeName = alias.Symbol.GetSymbolName()
                }
            };
        }
    }
}
