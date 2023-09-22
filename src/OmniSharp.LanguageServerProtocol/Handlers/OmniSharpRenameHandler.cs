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
using OmniSharp.Models.Rename;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal class OmniSharpRenameHandler : RenameHandlerBase
    {
        private readonly Mef.IRequestHandler<RenameRequest, RenameResponse> _renameHandler;
        private readonly TextDocumentSelector _documentSelector;

        public OmniSharpRenameHandler(Mef.IRequestHandler<RenameRequest, RenameResponse> renameHandler, TextDocumentSelector documentSelector)
        {
            _renameHandler = renameHandler;
            _documentSelector = documentSelector;
        }

        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<RenameRequest, RenameResponse>>())
                if (handler != null)
                    yield return new OmniSharpRenameHandler(handler, selector);
        }

        public override async Task<WorkspaceEdit> Handle(RenameParams request, CancellationToken token)
        {
            var omnisharpRequest = new RenameRequest
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                RenameTo = request.NewName,
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line),
                Buffer = request.NewName,
                WantsTextChanges = true,
                ApplyTextChanges = false
            };

            var omnisharpResponse = await _renameHandler.Handle(omnisharpRequest);

            if (omnisharpResponse.ErrorMessage != null)
            {
                return new WorkspaceEdit();
            }

            var changes = omnisharpResponse.Changes.ToDictionary(change =>
                Helpers.ToUri(change.FileName),
                x => x.Changes.Select(edit => new TextEdit
                {
                    NewText = edit.NewText,
                    Range = Helpers.ToRange((edit.StartColumn, edit.StartLine), (edit.EndColumn, edit.EndLine))
                }));

            return new WorkspaceEdit
            {
                Changes = changes
            };
        }

        protected override RenameRegistrationOptions CreateRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities)
        {
            return new RenameRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                PrepareProvider = false
            };
        }
    }
}
