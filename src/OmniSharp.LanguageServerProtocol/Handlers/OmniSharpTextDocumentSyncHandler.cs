using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Models;
using OmniSharp.Models.FileClose;
using OmniSharp.Models.FileOpen;
using OmniSharp.Models.UpdateBuffer;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class OmniSharpTextDocumentSyncHandler : TextDocumentSyncHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(
            RequestHandlers handlers,
            OmniSharpWorkspace workspace,
            DocumentVersions documentVersions,
            Task<MediatR.Unit> initialized)
        {
            foreach (var (selector, openHandler, closeHandler, bufferHandler) in handlers
                .OfType<
                    Mef.IRequestHandler<FileOpenRequest, FileOpenResponse>,
                    Mef.IRequestHandler<FileCloseRequest, FileCloseResponse>,
                    Mef.IRequestHandler<UpdateBufferRequest, object>>())
            {
                // TODO: Fix once cake has working support for incremental
                var documentSyncKind = TextDocumentSyncKind.Incremental;
                if (selector.ToString().IndexOf(".cake") > -1) documentSyncKind = TextDocumentSyncKind.Full;
                yield return new OmniSharpTextDocumentSyncHandler(openHandler, closeHandler, bufferHandler, selector, documentSyncKind, workspace, documentVersions, initialized);
            }
        }

        // TODO Make this configurable?
        private readonly Mef.IRequestHandler<FileOpenRequest, FileOpenResponse> _openHandler;
        private readonly Mef.IRequestHandler<FileCloseRequest, FileCloseResponse> _closeHandler;
        private readonly Mef.IRequestHandler<UpdateBufferRequest, object> _bufferHandler;
        private readonly OmniSharpWorkspace _workspace;
        private readonly DocumentVersions _documentVersions;
        private readonly Task<Unit> _serverInitialized;

        public OmniSharpTextDocumentSyncHandler(
            Mef.IRequestHandler<FileOpenRequest, FileOpenResponse> openHandler,
            Mef.IRequestHandler<FileCloseRequest, FileCloseResponse> closeHandler,
            Mef.IRequestHandler<UpdateBufferRequest, object> bufferHandler,
            DocumentSelector documentSelector,
            TextDocumentSyncKind documentSyncKind,
            OmniSharpWorkspace workspace,
            DocumentVersions documentVersions,
            Task<MediatR.Unit> initialized)
            : base(documentSyncKind, new TextDocumentSaveRegistrationOptions()
            {
                DocumentSelector = documentSelector,
                IncludeText = true,
            })
        {
            _openHandler = openHandler;
            _closeHandler = closeHandler;
            _bufferHandler = bufferHandler;
            _workspace = workspace;
            _documentVersions = documentVersions;
            _serverInitialized = initialized;
        }

        public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
        {
            var document = _workspace.GetDocument(Helpers.FromUri(uri));
            var langaugeId = "csharp";
            if (document == null) return new TextDocumentAttributes(uri, uri.Scheme, langaugeId);
            return new TextDocumentAttributes(uri, uri.Scheme, langaugeId);
        }

        public async override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken cancellationToken)
        {
            await _serverInitialized;
            if (notification.ContentChanges == null)
            {
                return Unit.Value;
            }
            var contentChanges = notification.ContentChanges.ToArray();
            if (contentChanges.Length == 1 && contentChanges[0].Range == null)
            {
                var change = contentChanges[0];
                await _bufferHandler.Handle(new UpdateBufferRequest()
                {
                    FileName = Helpers.FromUri(notification.TextDocument.Uri),
                    Buffer = change.Text
                });

                return Unit.Value;
            }

            var changes = contentChanges
                .Select(change => new LinePositionSpanTextChange()
                {
                    NewText = change.Text,
                    StartColumn = Convert.ToInt32(change.Range.Start.Character),
                    StartLine = Convert.ToInt32(change.Range.Start.Line),
                    EndColumn = Convert.ToInt32(change.Range.End.Character),
                    EndLine = Convert.ToInt32(change.Range.End.Line),
                })
                .ToArray();

            await _bufferHandler.Handle(new UpdateBufferRequest()
            {
                FileName = Helpers.FromUri(notification.TextDocument.Uri),
                Changes = changes
            });

            _documentVersions.Update(notification.TextDocument);

            return Unit.Value;
        }

        public async override Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken cancellationToken)
        {
            await _serverInitialized;
            if (_openHandler != null)
            {
                await _openHandler.Handle(new FileOpenRequest()
                {
                    Buffer = notification.TextDocument.Text,
                    FileName = Helpers.FromUri(notification.TextDocument.Uri)
                });

                _documentVersions.Reset(notification.TextDocument);
            }

            return Unit.Value;
        }

        public async override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken cancellationToken)
        {
            await _serverInitialized;
            if (_closeHandler != null)
            {
                await _closeHandler.Handle(new FileCloseRequest()
                {
                    FileName = Helpers.FromUri(notification.TextDocument.Uri)
                });

                _documentVersions.Remove(notification.TextDocument);
            }

            return Unit.Value;
        }

        public async override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken cancellationToken)
        {
            await _serverInitialized;
            if (Capability?.DidSave == true)
            {
                await _bufferHandler.Handle(new UpdateBufferRequest()
                {
                    FileName = Helpers.FromUri(notification.TextDocument.Uri),
                    Buffer = notification.Text
                });

                _documentVersions.Reset(notification.TextDocument);
            }
            return Unit.Value;
        }
    }
}
