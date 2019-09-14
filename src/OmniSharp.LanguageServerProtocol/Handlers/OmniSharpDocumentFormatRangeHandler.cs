using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.Format;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpDocumentFormatRangeHandler : DocumentRangeFormattingHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<FormatRangeRequest, FormatRangeResponse>>())
                if (handler != null)
                    yield return new OmniSharpDocumentFormatRangeHandler(handler, selector);
        }

        private readonly Mef.IRequestHandler<FormatRangeRequest, FormatRangeResponse> _formatRangeHandler;

        public OmniSharpDocumentFormatRangeHandler(Mef.IRequestHandler<FormatRangeRequest, FormatRangeResponse> formatRangeHandler, DocumentSelector documentSelector) : base(new TextDocumentRegistrationOptions()
        {
            DocumentSelector = documentSelector,
        })
        {
            _formatRangeHandler = formatRangeHandler;
        }

        public async override Task<TextEditContainer> Handle(DocumentRangeFormattingParams request, CancellationToken cancellationToken)
        {
            var omnisharpRequest = new FormatRangeRequest()
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                Line = Convert.ToInt32(request.Range.Start.Line),
                Column = Convert.ToInt32(request.Range.Start.Character),
                EndLine = Convert.ToInt32(request.Range.End.Line),
                EndColumn = Convert.ToInt32(request.Range.End.Character),
            };

            var omnisharpResponse = await _formatRangeHandler.Handle(omnisharpRequest);
            return omnisharpResponse.Changes.Select(change => new TextEdit()
            {
                NewText = change.NewText,
                Range = new Range(new Position(change.StartLine, change.StartColumn), new Position(change.EndLine, change.EndColumn))
            }).ToArray();
        }
    }
}
