using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Structure;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.V2;

namespace OmniSharp.Roslyn.CSharp.Services.Structure
{
    [OmniSharpHandler(OmniSharpEndpoints.V2.BlockStructure, LanguageNames.CSharp)]
    public class BlockStructureService : IRequestHandler<BlockStructureRequest, BlockStructureResponse>
    {
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public BlockStructureService(OmniSharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<BlockStructureResponse> Handle(BlockStructureRequest request)
        {
            // To provide complete code structure for the document wait until all projects are loaded.
            var document = await _workspace.GetDocumentFromFullProjectModelAsync(request.FileName);
            if (document == null)
            {
                return new BlockStructureResponse { Spans = Array.Empty<CodeFoldingBlock>() };
            }

            var text = await document.GetTextAsync();

            var structure = await OmniSharpBlockStructureService.GetBlockStructureAsync(document, CancellationToken.None);

            var outliningSpans = new List<CodeFoldingBlock>();
            foreach (var span in structure.Spans)
            {
                if (span.IsCollapsible)
                {
                    outliningSpans.Add(new CodeFoldingBlock(
                        text.GetRangeFromSpan(span.TextSpan),
                        type: ConvertToWellKnownBlockType(span.Type)));
                }
            }

            return new BlockStructureResponse() { Spans = outliningSpans };
        }

        private string ConvertToWellKnownBlockType(string kind)
        {
            return kind == CodeFoldingBlockKinds.Comment || kind == CodeFoldingBlockKinds.Imports
                ? kind
                : kind == OmniSharpBlockTypes.PreprocessorRegion
                    ? CodeFoldingBlockKinds.Region
                    : null;
        }
    }
}
