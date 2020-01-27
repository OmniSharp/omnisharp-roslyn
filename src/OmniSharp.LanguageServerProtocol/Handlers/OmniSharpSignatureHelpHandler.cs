using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.SignatureHelp;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal class OmniSharpSignatureHelpHandler : SignatureHelpHandler
    {
        private readonly Mef.IRequestHandler<SignatureHelpRequest, SignatureHelpResponse> _signatureHandler;

        public OmniSharpSignatureHelpHandler(Mef.IRequestHandler<SignatureHelpRequest, SignatureHelpResponse> signatureHandler, DocumentSelector documentSelector)
            : base(new SignatureHelpRegistrationOptions()
            {
                DocumentSelector = documentSelector,
                TriggerCharacters = new[] { ".", "?", "[" }
            })
        {
            _signatureHandler = signatureHandler;
        }

        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<SignatureHelpRequest, SignatureHelpResponse>>())
                if (handler != null)
                    yield return new OmniSharpSignatureHelpHandler(handler, selector);
        }

        public async override Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken token)
        {
            var omnisharpRequest = new SignatureHelpRequest
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line)
            };

            var omnisharpResponse = await _signatureHandler.Handle(omnisharpRequest);

            if (omnisharpResponse == null || !omnisharpResponse.Signatures.Any())
            {
                return null;
            }

            var containerSignatures = omnisharpResponse.Signatures.Select(x => new SignatureInformation
            {
                Documentation = x.Documentation,
                Label = x.Label,
                Parameters = new Container<ParameterInformation>(x.Parameters.Select(param => new ParameterInformation
                {
                    Documentation = param.Documentation,
                    Label = param.Label
                }))
            });

            var signatures = new Container<SignatureInformation>(containerSignatures);

            return new SignatureHelp
            {
                ActiveParameter = omnisharpResponse.ActiveParameter,
                ActiveSignature = omnisharpResponse.ActiveSignature,
                Signatures = signatures
            };
        }
    }
}
