using System;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
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
        private const int MethodLineOffset = 3;
        private const int PropertyLineOffset = 7;
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

            return await GetAliasFromMetadataAsync(new GotoDefinitionRequest
            {
                Line = response.Line,
                Column = response.Column,
                FileName = request.FileName,
                Timeout = request.Timeout,
                WantMetadata = true
            });
        }

        private async Task<GotoDefinitionResponse> GetAliasFromMetadataAsync(GotoDefinitionRequest request)
        {
            var document = Workspace.GetDocument(request.FileName);
            var response = new GotoDefinitionResponse();
            var lineIndex = request.Line + MethodLineOffset;
            var column = 0;

            if (document == null)
            {
                return response;
            }

            var semanticModel = await document.GetSemanticModelAsync();
            var sourceText = await document.GetTextAsync();
            var sourceLine = sourceText.Lines[lineIndex].ToString();
            if (sourceLine.Contains("(Context"))
            {
                column = sourceLine.IndexOf("(Context", StringComparison.Ordinal);
            }
            else
            {
                lineIndex = request.Line + PropertyLineOffset;
                sourceLine = sourceText.Lines[lineIndex].ToString();
                if (sourceLine.Contains("(Context"))
                {
                    column = sourceLine.IndexOf("(Context", StringComparison.Ordinal);
                }
                else
                {
                    return response;
                }
            }
            var position = sourceText.Lines.GetPosition(new LinePosition(lineIndex, column));
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, Workspace);

            if (symbol == null || symbol is INamespaceSymbol)
            {
                return response;
            }
            if (symbol is IMethodSymbol method)
            {
                symbol = method.PartialImplementationPart ?? symbol;
            }

            var location = symbol.Locations.First();

            if (!location.IsInMetadata)
            {
                return response;
            }
            var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(request.Timeout));
            var (metadataDocument, _) = await _metadataExternalSourceService.GetAndAddExternalSymbolDocument(document.Project, symbol, cancellationSource.Token);
            if (metadataDocument == null)
            {
                return response;
            }

            cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(request.Timeout));
            var metadataLocation = await _metadataExternalSourceService.GetExternalSymbolLocation(symbol, metadataDocument, cancellationSource.Token);
            var lineSpan = metadataLocation.GetMappedLineSpan();

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

            return response;
        }
    }
}
