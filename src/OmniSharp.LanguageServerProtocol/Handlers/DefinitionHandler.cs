using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Capabilities.Client;
using OmniSharp.Extensions.LanguageServer.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Mef;
using OmniSharp.Models.GotoDefinition;
using static OmniSharp.LanguageServerProtocol.Helpers;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    [Shared, Export(typeof(DefinitionHandler))]
    class DefinitionHandler : IDefinitionHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var group in handlers)
            {
                var handler = group.OfType<Mef.IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>>().SingleOrDefault();
                if (handler != null) yield return new DefinitionHandler(handler, group.DocumentSelector);
            }
        }

        private DefinitionCapability _capability;
        private readonly Mef.IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> _definitionHandler;
        private readonly DocumentSelector _documentSelector;

        [ImportingConstructor]
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

        public async Task<LocationOrLocations> Handle(TextDocumentPositionParams request, CancellationToken token)
        {
            var omnisharpRequest = new GotoDefinitionRequest()
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line)
            };

            var omnisharpResponse = await _definitionHandler.Handle(omnisharpRequest);

            return new LocationOrLocations(new Location()
            {
                Uri = Helpers.ToUri(omnisharpResponse.FileName),
                Range = ToRange((omnisharpResponse.Column, omnisharpResponse.Line))
            });
        }

        public void SetCapability(DefinitionCapability capability)
        {
            _capability = capability;
        }
    }
}
