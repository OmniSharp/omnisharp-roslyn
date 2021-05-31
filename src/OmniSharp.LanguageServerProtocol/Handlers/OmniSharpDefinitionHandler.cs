using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Models.V2.GotoDefinition;
using static OmniSharp.LanguageServerProtocol.Helpers;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class OmniSharpDefinitionHandler : DefinitionHandlerBase
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers.OfType<Mef.IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>>())
                if (handler != null)
                    yield return new OmniSharpDefinitionHandler(handler, selector);
        }

        private readonly Mef.IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> _definitionHandler;
        private readonly DocumentSelector _documentSelector;

        public OmniSharpDefinitionHandler(Mef.IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> definitionHandler, DocumentSelector documentSelector)
        {
            _definitionHandler = definitionHandler;
            _documentSelector = documentSelector;
        }

        public override async Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken token)
        {
            var omnisharpRequest = new GotoDefinitionRequest()
            {
                FileName = FromUri(request.TextDocument.Uri),
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line)
            };

            var omnisharpResponse = await _definitionHandler.Handle(omnisharpRequest);

            if (omnisharpResponse.Definitions == null)
            {
                return new LocationOrLocationLinks();
            }

            return new LocationOrLocationLinks(omnisharpResponse.Definitions.Select<Definition, LocationOrLocationLink>(definition => new Location()
            {
                Uri = definition.Location.FileName,
                Range = ToRange(definition.Location.Range)
            }));
        }

        protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DefinitionRegistrationOptions()
            {
                DocumentSelector = _documentSelector
            };
        }
    }
}
