using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Capabilities.Client;
using OmniSharp.Extensions.LanguageServer.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Mef;
using OmniSharp.Models.TypeLookup;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    [Shared, Export(typeof(HoverHandler))]
    class HoverHandler : IHoverHandler
    {
        private HoverCapability _capability;
        private readonly IRequestHandler<TypeLookupRequest, TypeLookupResponse> _definitionHandler;
        private readonly DocumentSelector _documentSelector;

        [ImportingConstructor]
        public HoverHandler(IEnumerable<IRequestHandler> handlers, DocumentSelector documentSelector)
        {
            _definitionHandler = handlers.OfType<IRequestHandler<TypeLookupRequest, TypeLookupResponse>>().Single();
            _documentSelector = documentSelector;
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector
            };
        }

        public async Task<Hover> Handle(TextDocumentPositionParams request, CancellationToken token)
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
                Contents = new MarkedStringContainer(omnisharpResponse.Type, omnisharpResponse.Documentation)
            };
        }

        public void SetCapability(HoverCapability capability)
        {
            _capability = capability;
        }
    }
}
