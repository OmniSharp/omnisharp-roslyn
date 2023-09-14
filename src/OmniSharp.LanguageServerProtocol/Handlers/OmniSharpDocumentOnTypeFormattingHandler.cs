using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.Format;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpDocumentOnTypeFormattingHandler : DocumentOnTypeFormattingHandlerBase
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<FormatAfterKeystrokeRequest, FormatRangeResponse>>())
                if (handler != null)
                    yield return new OmniSharpDocumentOnTypeFormattingHandler(handler, selector);
        }

        private readonly Mef.IRequestHandler<FormatAfterKeystrokeRequest, FormatRangeResponse> _formatAfterKeystrokeHandler;
        private readonly TextDocumentSelector _documentSelector;

        public OmniSharpDocumentOnTypeFormattingHandler(Mef.IRequestHandler<FormatAfterKeystrokeRequest, FormatRangeResponse> formatAfterKeystrokeHandler, TextDocumentSelector documentSelector)
        {
            _formatAfterKeystrokeHandler = formatAfterKeystrokeHandler;
            _documentSelector = documentSelector;
        }

        public override async Task<TextEditContainer> Handle(DocumentOnTypeFormattingParams request, CancellationToken cancellationToken)
        {
            // TODO: request.options
            var omnisharpRequest = new FormatAfterKeystrokeRequest()
            {
                Character = request.Character,
                Line = Convert.ToInt32(request.Position.Line),
                Column = Convert.ToInt32(request.Position.Character),
                FileName = Helpers.FromUri(request.TextDocument.Uri),
            };

            var omnisharpResponse = await _formatAfterKeystrokeHandler.Handle(omnisharpRequest);
            return omnisharpResponse.Changes.Select(change => new TextEdit()
            {
                NewText = change.NewText,
                Range = new Range(new Position(change.StartLine, change.StartColumn), new Position(change.EndLine, change.EndColumn))
            }).ToArray();
        }

        protected override DocumentOnTypeFormattingRegistrationOptions CreateRegistrationOptions(DocumentOnTypeFormattingCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DocumentOnTypeFormattingRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                // Chose these triggers based on Roslyn's implementation https://github.com/dotnet/roslyn/blob/9e06c76c5ce94dc49821c5bd211c8292b3a984f0/src/Features/LanguageServer/Protocol/DefaultCapabilitiesProvider.cs#L71
                FirstTriggerCharacter = "}",
                MoreTriggerCharacter = new[] { ";", "\n" }
            };
        }
    }
}
