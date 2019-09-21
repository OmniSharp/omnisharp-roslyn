using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.TypeLookup;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class OmniSharpHoverHandler : HoverHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<TypeLookupRequest, TypeLookupResponse>>())
                if (handler != null)
                    yield return new OmniSharpHoverHandler(handler, selector);
        }

        private readonly Mef.IRequestHandler<TypeLookupRequest, TypeLookupResponse> _definitionHandler;

        public OmniSharpHoverHandler(Mef.IRequestHandler<TypeLookupRequest, TypeLookupResponse> definitionHandler, DocumentSelector documentSelector)
            : base(new TextDocumentRegistrationOptions()
            {
                DocumentSelector = documentSelector
            })
        {
            _definitionHandler = definitionHandler;
        }

        public async override Task<Hover> Handle(HoverParams request, CancellationToken token)
        {
            var omnisharpRequest = new TypeLookupRequest()
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line),
                IncludeDocumentation = true
            };

            var omnisharpResponse = await _definitionHandler.Handle(omnisharpRequest);

            return new Hover()
            {
                // TODO: Range?  We don't currently have that!
                // Range =
                Contents = new MarkedStringsOrMarkupContent(new MarkedStringContainer(Helpers.EscapeMarkdown(omnisharpResponse.Type), Helpers.EscapeMarkdown(omnisharpResponse.Documentation)))
            };
        }
    }
}
