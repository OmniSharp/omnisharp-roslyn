using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.GotoDefinition;
using static OmniSharp.LanguageServerProtocol.Helpers;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class DefinitionHandler : IDefinitionHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers.OfType<Mef.IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>>())
                if (handler != null)
                    yield return new DefinitionHandler(handler, selector);
        }

        private DefinitionCapability _capability;
        private readonly Mef.IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> _definitionHandler;
        private readonly DocumentSelector _documentSelector;

        public DefinitionHandler(Mef.IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> definitionHandler, DocumentSelector documentSelector)
        {
            _definitionHandler = definitionHandler;
            _documentSelector = documentSelector;
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector
            };
        }

        public async Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken token)
        {
            var omnisharpRequest = new GotoDefinitionRequest()
            {
                FileName = FromUri(request.TextDocument.Uri),
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line)
            };

            var omnisharpResponse = await _definitionHandler.Handle(omnisharpRequest);

            if (string.IsNullOrWhiteSpace(omnisharpResponse.FileName))
            {
                return new LocationOrLocationLinks();
            }

            return new LocationOrLocationLinks(new Location()
            {
                Uri = ToUri(omnisharpResponse.FileName),
                Range = ToRange((omnisharpResponse.Column, omnisharpResponse.Line))
            });
        }

        public void SetCapability(DefinitionCapability capability)
        {
            _capability = capability;
        }
    }
}
