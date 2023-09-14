using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.MembersTree;
using OmniSharp.Models.V2.CodeStructure;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpDocumentSymbolHandler : DocumentSymbolHandlerBase
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<CodeStructureRequest, CodeStructureResponse>>())
                if (handler != null)
                    yield return new OmniSharpDocumentSymbolHandler(handler, selector);
        }

        private readonly Mef.IRequestHandler<CodeStructureRequest, CodeStructureResponse> _codeStructureHandler;
        private readonly TextDocumentSelector _documentSelector;

        public OmniSharpDocumentSymbolHandler(Mef.IRequestHandler<CodeStructureRequest, CodeStructureResponse> codeStructureHandler, TextDocumentSelector documentSelector)
        {
            _codeStructureHandler = codeStructureHandler;
            _documentSelector = documentSelector;
        }

        public override async Task<SymbolInformationOrDocumentSymbolContainer> Handle(DocumentSymbolParams request, CancellationToken token)
        {
            var omnisharpRequest = new CodeStructureRequest()
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
            };

            var omnisharpResponse = await _codeStructureHandler.Handle(omnisharpRequest);

            return omnisharpResponse.Elements?.Select(ToDocumentSymbolInformationOrDocumentSymbol).ToArray() ??
                Array.Empty<SymbolInformationOrDocumentSymbol>();
        }

        private static SymbolInformationOrDocumentSymbol ToDocumentSymbolInformationOrDocumentSymbol(CodeElement node)
        {
            return new SymbolInformationOrDocumentSymbol(ToDocumentSymbol(node));
        }

        private static DocumentSymbol ToDocumentSymbol(CodeElement node)
        {
            return new DocumentSymbol
            {
                Name = node.Name,
                Kind = Helpers.ToSymbolKind(node.Kind),
                Range = Helpers.ToRange(node.Ranges[OmniSharp.Models.V2.SymbolRangeNames.Full]),
                SelectionRange = Helpers.ToRange(node.Ranges[OmniSharp.Models.V2.SymbolRangeNames.Name]),
                Children = new Container<DocumentSymbol>(node.Children?.Select(ToDocumentSymbol) ?? Enumerable.Empty<DocumentSymbol>())
            };
        }

        protected override DocumentSymbolRegistrationOptions CreateRegistrationOptions(DocumentSymbolCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DocumentSymbolRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }
    }
}
