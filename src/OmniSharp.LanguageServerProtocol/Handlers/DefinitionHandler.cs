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

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    [Shared, Export(typeof(DefinitionHandler))]
    class DefinitionHandler : IDefinitionHandler
    {
        private DefinitionCapability _capability;
        private readonly IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> _definitionHandler;
        private readonly DocumentSelector _documentSelector;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public DefinitionHandler(IEnumerable<IRequestHandler> handlers, DocumentSelector documentSelector, ILogger logger)
        {
            _definitionHandler = handlers.OfType<IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>>().Single();
            _documentSelector = documentSelector;
            _logger = logger;
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

            _logger.LogInformation(JsonConvert.SerializeObject(omnisharpRequest));

            var omnisharpResponse = await _definitionHandler.Handle(omnisharpRequest);

            _logger.LogInformation(JsonConvert.SerializeObject(omnisharpResponse));

            return new LocationOrLocations(new Location()
            {
                Uri = Helpers.ToUri(omnisharpResponse.FileName),
                Range = new Range()
                {
                    Start = new Position()
                    {
                        Character = omnisharpResponse.Column,
                        Line = omnisharpResponse.Line,
                    },
                    End = new Position()
                    {
                        Character = omnisharpResponse.Column,
                        Line = omnisharpResponse.Line,
                    }
                }
            });
        }

        public void SetCapability(DefinitionCapability capability)
        {
            _capability = capability;
        }
    }
}
