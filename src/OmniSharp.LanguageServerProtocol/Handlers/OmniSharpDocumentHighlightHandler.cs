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
using OmniSharp.Models.FindUsages;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpDocumentHighlightHandler : DocumentHighlightHandlerBase
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<FindUsagesRequest, QuickFixResponse>>())
                if (handler != null)
                    yield return new OmniSharpDocumentHighlightHandler(handler, selector);
        }

        private readonly Mef.IRequestHandler<FindUsagesRequest, QuickFixResponse> _findUsagesHandler;
        private readonly TextDocumentSelector _documentSelector;

        public OmniSharpDocumentHighlightHandler(Mef.IRequestHandler<FindUsagesRequest, QuickFixResponse> findUsagesHandler, TextDocumentSelector documentSelector)
        {
            _findUsagesHandler = findUsagesHandler;
            _documentSelector = documentSelector;
        }

        public override async Task<DocumentHighlightContainer> Handle(DocumentHighlightParams request, CancellationToken token)
        {
            // TODO: Utilize Roslyn ExternalAccess to take advantage of HighlightingService.

            var omnisharpRequest = new FindUsagesRequest
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line),
                OnlyThisFile = true,
                ExcludeDefinition = false
            };

            var omnisharpResponse = await _findUsagesHandler.Handle(omnisharpRequest);

            if (omnisharpResponse.QuickFixes is null)
            {
                return new DocumentHighlightContainer();
            }

            return new DocumentHighlightContainer(omnisharpResponse.QuickFixes.Select(x => new DocumentHighlight
            {
                Kind = DocumentHighlightKind.Read,
                Range = x.ToRange()
            }));
        }

        protected override DocumentHighlightRegistrationOptions CreateRegistrationOptions(DocumentHighlightCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DocumentHighlightRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }
    }
}
