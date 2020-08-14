using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Models;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class OmniSharpHoverHandler : HoverHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<QuickInfoRequest, QuickInfoResponse>>())
                if (handler != null)
                    yield return new OmniSharpHoverHandler(handler, selector);
        }

        private readonly Mef.IRequestHandler<QuickInfoRequest, QuickInfoResponse> _definitionHandler;

        public OmniSharpHoverHandler(Mef.IRequestHandler<QuickInfoRequest, QuickInfoResponse> definitionHandler, DocumentSelector documentSelector)
            : base(new HoverRegistrationOptions()
            {
                DocumentSelector = documentSelector
            })
        {
            _definitionHandler = definitionHandler;
        }

        public async override Task<Hover> Handle(HoverParams request, CancellationToken token)
        {
            var omnisharpRequest = new QuickInfoRequest()
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line),
            };

            var omnisharpResponse = await _definitionHandler.Handle(omnisharpRequest);

            return new Hover()
            {
                // TODO: Range?  We don't currently have that!
                // Range =
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent() { Value = omnisharpResponse.Markdown })
            };
        }
    }
}
