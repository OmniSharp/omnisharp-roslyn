using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Models;
using OmniSharp.Models.FindImplementations;
using static OmniSharp.LanguageServerProtocol.Helpers;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpImplementationHandler : ImplementationHandlerBase
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<FindImplementationsRequest, QuickFixResponse>>())
                if (handler != null)
                    yield return new OmniSharpImplementationHandler(handler, selector);
        }

        private readonly Mef.IRequestHandler<FindImplementationsRequest, QuickFixResponse> _findImplementationsHandler;
        private readonly TextDocumentSelector _documentSelector;

        public OmniSharpImplementationHandler(Mef.IRequestHandler<FindImplementationsRequest, QuickFixResponse> findImplementationsHandler, TextDocumentSelector documentSelector)
        {
            _findImplementationsHandler = findImplementationsHandler;
            _documentSelector = documentSelector;
        }

        public override async Task<LocationOrLocationLinks> Handle(ImplementationParams request, CancellationToken token)
        {
            var omnisharpRequest = new FindImplementationsRequest()
            {
                FileName = FromUri(request.TextDocument.Uri),
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line)
            };

            var omnisharpResponse = await _findImplementationsHandler.Handle(omnisharpRequest);

            return omnisharpResponse?.QuickFixes?.Select(x => new LocationOrLocationLink(new Location
            {
                Uri = ToUri(x.FileName),
                Range = ToRange((x.Column, x.Line))
            })).ToArray() ?? Array.Empty<LocationOrLocationLink>();
        }

        protected override ImplementationRegistrationOptions CreateRegistrationOptions(ImplementationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new ImplementationRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }
    }
}
