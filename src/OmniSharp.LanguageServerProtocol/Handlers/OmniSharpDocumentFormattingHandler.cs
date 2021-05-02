using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.CodeFormat;
using OmniSharp.Models.MembersTree;
using OmniSharp.Models.V2.CodeStructure;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpDocumentFormattingHandler : DocumentFormattingHandlerBase
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<CodeFormatRequest, CodeFormatResponse>>())
                if (handler != null)
                    yield return new OmniSharpDocumentFormattingHandler(handler, selector);
        }

        private readonly Mef.IRequestHandler<CodeFormatRequest, CodeFormatResponse> _codeFormatHandler;
        private readonly DocumentSelector _documentSelector;

        public OmniSharpDocumentFormattingHandler(Mef.IRequestHandler<CodeFormatRequest, CodeFormatResponse> codeFormatHandler, DocumentSelector documentSelector)
        {
            _codeFormatHandler = codeFormatHandler;
            _documentSelector = documentSelector;
        }

        public override async Task<TextEditContainer> Handle(DocumentFormattingParams request, CancellationToken cancellationToken)
        {
            var omnisharpRequest = new CodeFormatRequest()
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                WantsTextChanges = true
            };

            var omnisharpResponse = await _codeFormatHandler.Handle(omnisharpRequest);
            return omnisharpResponse.Changes.Select(change => new TextEdit()
            {
                NewText = change.NewText,
                Range = new Range(new Position(change.StartLine, change.StartColumn), new Position(change.EndLine, change.EndColumn))
            }).ToArray();
        }

        protected override DocumentFormattingRegistrationOptions CreateRegistrationOptions(DocumentFormattingCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DocumentFormattingRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }
    }
}
