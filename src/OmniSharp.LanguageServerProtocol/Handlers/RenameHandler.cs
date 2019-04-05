using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.Rename;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal class RenameHandler : IRenameHandler
    {
        private readonly Mef.IRequestHandler<RenameRequest, RenameResponse> _renameHandler;
        private readonly DocumentSelector _documentSelector;
        private RenameCapability _capability;

        public RenameHandler(Mef.IRequestHandler<RenameRequest, RenameResponse> renameHandler, DocumentSelector documentSelector)
        {
            _renameHandler = renameHandler;
            _documentSelector = documentSelector;
        }

        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<RenameRequest, RenameResponse>>())
                if (handler != null)
                    yield return new RenameHandler(handler, selector);
        }

        public async Task<WorkspaceEdit> Handle(RenameParams request, CancellationToken token)
        {
            var omnisharpRequest = new RenameRequest
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                RenameTo = request.NewName,
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line),
                Buffer = request.NewName
            };

            var omnisharpResponse = await _renameHandler.Handle(omnisharpRequest);

            if (omnisharpResponse.ErrorMessage != null)
            {
            }

            var changes = omnisharpResponse.Changes.ToDictionary(change => Helpers.ToUri(change.FileName),
                x => x.Changes.Select(edit => new TextEdit
                {
                    NewText = edit.NewText,
                    Range = Helpers.ToRange((edit.StartColumn, edit.StartLine), (edit.EndColumn, edit.EndLine))
                }));

            var edits = changes.Values.SelectMany(edit => edit.ToList());

            var documentEdits = omnisharpResponse.Changes.Select(x => new WorkspaceEditDocumentChange(
                new TextDocumentEdit
                {
                    Edits = new Container<TextEdit>(edits),
                    TextDocument = new VersionedTextDocumentIdentifier
                    {
                        Uri = Helpers.ToUri(x.FileName)
                    }
                }));

            return new WorkspaceEdit
            {
                Changes = changes,
                DocumentChanges = new Container<WorkspaceEditDocumentChange>(documentEdits)
            };
        }

        public RenameRegistrationOptions GetRegistrationOptions()
        {
            return new RenameRegistrationOptions
            {
                DocumentSelector = _documentSelector
            };
        }

        public void SetCapability(RenameCapability capability)
        {
            _capability = capability;
        }
    }
}
