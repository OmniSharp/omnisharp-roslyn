using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Models.V2;
using static OmniSharp.LanguageServerProtocol.Helpers;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class OmniSharpFoldingRangenHandler : FoldingRangeHandlerBase
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers.OfType<Mef.IRequestHandler<BlockStructureRequest, BlockStructureResponse>>())
                if (handler != null)
                    yield return new OmniSharpFoldingRangenHandler(handler, selector);
        }

        private readonly Mef.IRequestHandler<BlockStructureRequest, BlockStructureResponse> _definitionHandler;
        private readonly TextDocumentSelector _documentSelector;

        public OmniSharpFoldingRangenHandler(Mef.IRequestHandler<BlockStructureRequest, BlockStructureResponse> definitionHandler, TextDocumentSelector documentSelector)
        {
            _definitionHandler = definitionHandler;
            _documentSelector = documentSelector;
        }

        public override async Task<Container<FoldingRange>> Handle(FoldingRangeRequestParam request, CancellationToken token)
        {
            var omnisharpRequest = new BlockStructureRequest()
            {
                FileName = FromUri(request.TextDocument.Uri)
            };

            var omnisharpResponse = await _definitionHandler.Handle(omnisharpRequest);

            if (omnisharpResponse.Spans is null)
            {
                return new Container<FoldingRange>();
            }

            return new Container<FoldingRange>(omnisharpResponse.Spans.Select(block => new FoldingRange()
            {
                StartLine = block.Range.Start.Line,
                StartCharacter = block.Range.Start.Column,
                EndLine = block.Range.End.Line,
                EndCharacter = block.Range.End.Column,
                Kind = ConvertKind(block.Kind),
            }));
        }

        private static FoldingRangeKind? ConvertKind(string kind)
        {
            return kind switch
            {
                CodeFoldingBlockKinds.Comment => FoldingRangeKind.Comment,
                CodeFoldingBlockKinds.Imports => FoldingRangeKind.Imports,
                CodeFoldingBlockKinds.Region => FoldingRangeKind.Region,
                _ => null
            };
        }

        protected override FoldingRangeRegistrationOptions CreateRegistrationOptions(FoldingRangeCapability capability, ClientCapabilities clientCapabilities)
        {
            return new FoldingRangeRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }
    }
}
