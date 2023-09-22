using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Models.GotoTypeDefinition;
using static OmniSharp.LanguageServerProtocol.Helpers;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class OmniSharpTypeDefinitionHandler : TypeDefinitionHandlerBase
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers.OfType<Mef.IRequestHandler<GotoTypeDefinitionRequest, GotoTypeDefinitionResponse>>())
                if (handler != null)
                    yield return new OmniSharpTypeDefinitionHandler(handler, selector);
        }

        private readonly Mef.IRequestHandler<GotoTypeDefinitionRequest, GotoTypeDefinitionResponse> _definitionHandler;
        private readonly TextDocumentSelector _documentSelector;

        public OmniSharpTypeDefinitionHandler(Mef.IRequestHandler<GotoTypeDefinitionRequest, GotoTypeDefinitionResponse> definitionHandler, TextDocumentSelector documentSelector)
        {
            _definitionHandler = definitionHandler;
            _documentSelector = documentSelector;
        }

        public override async Task<LocationOrLocationLinks> Handle(TypeDefinitionParams request, CancellationToken token)
        {
            var omnisharpRequest = new GotoTypeDefinitionRequest()
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

            return new LocationOrLocationLinks(omnisharpResponse.Definitions.Select<TypeDefinition, LocationOrLocationLink>(definition => new Location()
            {
                Uri = definition.Location.FileName,
                Range = ToRange(definition.Location.Range)
            }));
        }

        protected override TypeDefinitionRegistrationOptions CreateRegistrationOptions(TypeDefinitionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TypeDefinitionRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }
    }
}
