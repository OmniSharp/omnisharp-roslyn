using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
        private DefinitionCapability _capability;
        private readonly IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> _definitionHandler;
        private readonly DocumentSelector _documentSelector;

        [ImportingConstructor]
        public DefinitionHandler(IEnumerable<IRequestHandler> handlers, DocumentSelector documentSelector)
        {
            _definitionHandler = handlers.OfType<IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>>().Single();
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
