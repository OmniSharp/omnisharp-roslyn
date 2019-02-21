using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models;
using OmniSharp.Models.FindUsages;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class ReferencesHandler : IReferencesHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<FindUsagesRequest, QuickFixResponse>>())
                if (handler != null)
                    yield return new ReferencesHandler(handler, selector);
        }

        private ReferencesCapability _capability;
        private readonly Mef.IRequestHandler<FindUsagesRequest, QuickFixResponse> _findUsagesHandler;
        private readonly DocumentSelector _documentSelector;

        public ReferencesHandler(Mef.IRequestHandler<FindUsagesRequest, QuickFixResponse> findUsagesHandler, DocumentSelector documentSelector)
        {
            _findUsagesHandler = findUsagesHandler;
            _documentSelector = documentSelector;
        }

        public async Task<LocationContainer> Handle(ReferenceParams request, CancellationToken token)
        {
            var omnisharpRequest = new FindUsagesRequest
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line),
                OnlyThisFile = false,
                ExcludeDefinition = !request.Context.IncludeDeclaration
            };

            var omnisharpResponse = await _findUsagesHandler.Handle(omnisharpRequest);

            return omnisharpResponse.QuickFixes?.Select(x => new Location
            {
                Uri = Helpers.ToUri(x.FileName),
                Range = x.ToRange()
            }).ToArray();
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector
            };
        }

        public void SetCapability(ReferencesCapability capability)
        {
            _capability = capability;
        }
    }
}
