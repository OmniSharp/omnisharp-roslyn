using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Abstractions;
using OmniSharp.Extensions.LanguageServer.Capabilities.Client;
using OmniSharp.Extensions.LanguageServer.Capabilities.Server;
using OmniSharp.Extensions.LanguageServer.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FileClose;
using OmniSharp.Models.FileOpen;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Roslyn;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class TextDocumentSyncHandler : ITextDocumentSyncHandler, IWillSaveTextDocumentHandler, IWillSaveWaitUntilTextDocumentHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(
            RequestHandlers handlers,
            OmniSharpWorkspace workspace)
        {
            foreach (var (selector, openHandler, closeHandler, bufferHandler) in handlers
                .OfType<
                    Mef.IRequestHandler<FileOpenRequest, FileOpenResponse>,
                    Mef.IRequestHandler<FileCloseRequest, FileCloseResponse>,
                    Mef.IRequestHandler<UpdateBufferRequest, object>>())
            {
                // TODO: Fix once cake has working support for incremental
                var documentSyncKind = TextDocumentSyncKind.Incremental;
                // if (selector.ToString().IndexOf(".cake") > -1) documentSyncKind = TextDocumentSyncKind.Full;
                yield return new TextDocumentSyncHandler(openHandler, closeHandler, bufferHandler, selector, documentSyncKind, workspace);
            }
        }

        // TODO Make this configurable?
        private readonly DocumentSelector _documentSelector;
        private SynchronizationCapability _capability;
        private readonly Mef.IRequestHandler<FileOpenRequest, FileOpenResponse> _openHandler;
        private readonly Mef.IRequestHandler<FileCloseRequest, FileCloseResponse> _closeHandler;
        private readonly Mef.IRequestHandler<UpdateBufferRequest, object> _bufferHandler;
        private readonly OmniSharpWorkspace _workspace;

        public TextDocumentSyncHandler(
            Mef.IRequestHandler<FileOpenRequest, FileOpenResponse> openHandler,
            Mef.IRequestHandler<FileCloseRequest, FileCloseResponse> closeHandler,
            Mef.IRequestHandler<UpdateBufferRequest, object> bufferHandler,
            DocumentSelector documentSelector,
            TextDocumentSyncKind documentSyncKind,
            OmniSharpWorkspace workspace)
        {
            _openHandler = openHandler;
            _closeHandler = closeHandler;
            _bufferHandler = bufferHandler;
            _workspace = workspace;
            _documentSelector = documentSelector;
            Options.Change = documentSyncKind;
        }

        public TextDocumentSyncOptions Options { get; } = new TextDocumentSyncOptions()
        {
            Change = TextDocumentSyncKind.Incremental,
            OpenClose = true,
            WillSave = false, // Do we need to configure this?
            WillSaveWaitUntil = false,  // Do we need to configure this?
            Save = new SaveOptions()
            {
                IncludeText = true
            }
        };

        public TextDocumentAttributes GetTextDocumentAttributes(Uri uri)
        {
            var document = _workspace.GetDocument(Helpers.FromUri(uri));
            if (document == null) return new TextDocumentAttributes(uri, "");
            return new TextDocumentAttributes(uri, "");
        }

        public Task Handle(DidChangeTextDocumentParams notification)
        {
            var contentChanges = notification.ContentChanges.ToArray();
            if (contentChanges.Length == 1 && contentChanges[0].Range == null)
            {
                var change = contentChanges[0];
                return _bufferHandler.Handle(new UpdateBufferRequest()
                {
                    FileName = Helpers.FromUri(notification.TextDocument.Uri),
                    Buffer = change.Text
                });
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

            return _bufferHandler.Handle(new UpdateBufferRequest()
            {
                FileName = Helpers.FromUri(notification.TextDocument.Uri),
                Changes = changes
            });
        }

        public Task Handle(DidOpenTextDocumentParams notification)
        {
            return _openHandler?.Handle(new FileOpenRequest()
            {
                Buffer = notification.TextDocument.Text,
                FileName = Helpers.FromUri(notification.TextDocument.Uri)
            }) ?? Task.CompletedTask;
        }

        public Task Handle(DidCloseTextDocumentParams notification)
        {
            return _closeHandler?.Handle(new FileCloseRequest()
            {
                FileName = Helpers.FromUri(notification.TextDocument.Uri)
            }) ?? Task.CompletedTask;
        }

        public Task Handle(DidSaveTextDocumentParams notification)
        {
            if (_capability?.DidSave == true)
            {
                return _bufferHandler.Handle(new UpdateBufferRequest()
                {
                    FileName = Helpers.FromUri(notification.TextDocument.Uri),
                    Buffer = notification.Text
                });
            }
            return Task.CompletedTask;
        }

        public Task Handle(WillSaveTextDocumentParams notification)
        {
            // TODO: Do we have a need for this?
            if (_capability?.WillSave == true) { }
            return Task.CompletedTask;
        }

        public Task Handle(WillSaveTextDocumentParams request, CancellationToken token)
        {
            // TODO: Do we have a need for this?
            if (_capability?.WillSaveWaitUntil == true) { }
            return Task.CompletedTask;
        }

        public void SetCapability(SynchronizationCapability capability)
        {
            _capability = capability;
        }

        TextDocumentChangeRegistrationOptions IRegistration<TextDocumentChangeRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentChangeRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                SyncKind = Options.Change
            };
        }

        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentSaveRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                IncludeText = true
            };
        }
    }
}
