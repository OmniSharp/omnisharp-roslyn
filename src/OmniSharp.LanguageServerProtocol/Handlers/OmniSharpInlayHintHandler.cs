using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Models.v1.InlayHints;
using static OmniSharp.LanguageServerProtocol.Helpers;
using LSPInlayHint = OmniSharp.Extensions.LanguageServer.Protocol.Models.InlayHint;
using LSPInlayHintKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.InlayHintKind;
using OmniSharpInlayHint = OmniSharp.Models.v1.InlayHints.InlayHint;
using OmniSharpInlayHintKind = OmniSharp.Models.v1.InlayHints.InlayHintKind;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class OmniSharpInlayHintHandler : InlayHintsHandlerBase
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler, resolveHandler) in handlers
                .OfType<Mef.IRequestHandler<InlayHintRequest, InlayHintResponse>, Mef.IRequestHandler<InlayHintResolveRequest, OmniSharpInlayHint>>())
            {
                if (handler != null && resolveHandler != null)
                    yield return new OmniSharpInlayHintHandler(handler, resolveHandler, selector);
            }
        }

        private readonly Mef.IRequestHandler<InlayHintRequest, InlayHintResponse> _inlayHintHandler;
        private readonly Mef.IRequestHandler<InlayHintResolveRequest, OmniSharpInlayHint> _inlayHintResolveHandler;
        private readonly TextDocumentSelector _documentSelector;

        public OmniSharpInlayHintHandler(
            Mef.IRequestHandler<InlayHintRequest, InlayHintResponse> inlayHintHandler,
            Mef.IRequestHandler<InlayHintResolveRequest, OmniSharpInlayHint> inlayHintResolveHandler,
            TextDocumentSelector documentSelector)
        {
            _inlayHintHandler = inlayHintHandler;
            _inlayHintResolveHandler = inlayHintResolveHandler;
            _documentSelector = documentSelector;
        }

        public override async Task<InlayHintContainer> Handle(InlayHintParams request, CancellationToken cancellationToken)
        {
            var omnisharpRequest = new InlayHintRequest()
            {
                Location = new Models.V2.Location()
                {
                    FileName = FromUri(request.TextDocument.Uri),
                    Range = FromRange(request.Range),
                }
            };

            var omnisharpResponse = await _inlayHintHandler.Handle(omnisharpRequest);

            return new InlayHintContainer(omnisharpResponse.InlayHints.Select(ToLSPInlayHint));
        }

        public override async Task<LSPInlayHint> Handle(LSPInlayHint request, CancellationToken cancellationToken)
        {
            var omnisharpRequest = new InlayHintResolveRequest()
            {
                Hint = FromLSPInlayHint(request)
            };

            var omnisharpResponse = await _inlayHintResolveHandler.Handle(omnisharpRequest);

            return ToLSPInlayHint(omnisharpResponse);
        }

        private static LSPInlayHint ToLSPInlayHint(OmniSharpInlayHint hint)
        {
            var trimmedStartLabel = hint.Label.TrimStart();
            var trimmedLabel = trimmedStartLabel.TrimEnd();
            return new LSPInlayHint()
            {
                Label = trimmedLabel,
                Kind = hint.Kind.HasValue ? ConvertEnum<OmniSharpInlayHintKind, LSPInlayHintKind>(hint.Kind.Value) : null,
                Tooltip = hint.Tooltip is not null
                    ? new MarkupContent() { Kind = MarkupKind.Markdown, Value = hint.Tooltip }
                    : null,
                Position = ToPosition(hint.Position),
                TextEdits = hint.TextEdits is not null ? new(ToTextEdits(hint.TextEdits)) : null,
                PaddingLeft = hint.Label.Length > trimmedStartLabel.Length,
                PaddingRight = trimmedStartLabel.Length > trimmedLabel.Length,
                Data = JToken.FromObject(hint.Data),
            };
        }

        private static OmniSharpInlayHint FromLSPInlayHint(LSPInlayHint hint)
        {
            return new OmniSharpInlayHint()
            {
                Label = $"{(hint.PaddingLeft == true ? " " : "")}{hint.Label.String}{(hint.PaddingRight == true ? " " : "")}",
                Kind = hint.Kind.HasValue ? ConvertEnum<LSPInlayHintKind, OmniSharpInlayHintKind>(hint.Kind.Value) : null,
                Tooltip = hint.Tooltip is not null
                    ? hint.Tooltip.HasMarkupContent
                        ? hint.Tooltip.MarkupContent.Value
                        : hint.Tooltip.String
                    : null,
                Position = FromPosition(hint.Position),
                TextEdits = hint.TextEdits is null ? null : FromTextEdits(hint.TextEdits),
                Data = hint.Data.ToObject<(string SolutionVersion, int Position)>()
            };
        }

        protected override InlayHintRegistrationOptions CreateRegistrationOptions(InlayHintClientCapabilities capability, ClientCapabilities clientCapabilities)
        {
            return new InlayHintRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                ResolveProvider = true,
            };
        }
    }
}
