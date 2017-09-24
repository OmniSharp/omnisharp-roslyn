using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Mef;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Abstractions;
using OmniSharp.Extensions.LanguageServer.Capabilities.Client;
using OmniSharp.Extensions.LanguageServer.Capabilities.Server;
using OmniSharp.Extensions.LanguageServer.Models;
using OmniSharp.Models.FileOpen;
using OmniSharp.Models.FileClose;
using OmniSharp.Roslyn;
using OmniSharp.Models;
using System.Composition;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.Models.GotoDefinition;
using static OmniSharp.LanguageServerProtocol.Helpers;

namespace OmniSharp.LanguageServerProtocol
{
    [Shared, Export(typeof(DefinitionHandler))]
    class DefinitionHandler : IDefinitionHandler
    {
        private DefinitionCapability _capability;
        private readonly IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse> _definitionHandler;
        private readonly DocumentSelector _documentSelector;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public DefinitionHandler(IEnumerable<IRequestHandler> handlers, DocumentSelector documentSelector, ILogger logger)
        {
            _definitionHandler = handlers.OfType<IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>>().Single();
            _documentSelector = documentSelector;
            _logger = logger;
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector
            };
        }

        public async Task<LocationOrLocations> Handle(TextDocumentPositionParams request, CancellationToken token)
        {
            var omnisharpRequest = new GotoDefinitionRequest()
            {
                FileName = FromUri(request.TextDocument.Uri),
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line)
            };

            _logger.LogInformation(JsonConvert.SerializeObject(omnisharpRequest));

            var omnisharpResponse = await _definitionHandler.Handle(omnisharpRequest);

            _logger.LogInformation(JsonConvert.SerializeObject(omnisharpResponse));

            return new LocationOrLocations(new Location()
            {
                Uri = ToUri(omnisharpResponse.FileName),
                Range = new Range()
                {
                    Start = new Position()
                    {
                        Character = omnisharpResponse.Column,
                        Line = omnisharpResponse.Line,
                    },
                    End = new Position()
                    {
                        Character = omnisharpResponse.Column,
                        Line = omnisharpResponse.Line,
                    }
                }
            });
        }

        public void SetCapability(DefinitionCapability capability)
        {
            _capability = capability;
        }
    }

    [Shared, Export(typeof(TextDocumentSyncHandler))]
    class TextDocumentSyncHandler : ITextDocumentSyncHandler, IWillSaveTextDocumentHandler, IWillSaveWaitUntilTextDocumentHandler
    {
        // TODO Make this configurable?
        private readonly DocumentSelector _documentSelector;
        private SynchronizationCapability _capability;
        private readonly IRequestHandler<FileOpenRequest, FileOpenResponse> _openHandler;
        private readonly IRequestHandler<FileCloseRequest, FileCloseResponse> _closeHandler;
        private readonly BufferManager _bufferManager;
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public TextDocumentSyncHandler(IEnumerable<IRequestHandler> handlers, DocumentSelector documentSelector, OmniSharpWorkspace workspace)
        {
            _openHandler = handlers.OfType<IRequestHandler<FileOpenRequest, FileOpenResponse>>().Single();
            _closeHandler = handlers.OfType<IRequestHandler<FileCloseRequest, FileCloseResponse>>().Single();
            _bufferManager = workspace.BufferManager;
            _workspace = workspace;
            _documentSelector = documentSelector;
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
            var document = _workspace.GetDocument(FromUri(uri));
            if (document == null) return new TextDocumentAttributes(uri, "");
            return new TextDocumentAttributes(uri, "csharp");
        }

        public Task Handle(DidChangeTextDocumentParams notification)
        {
            if (notification.ContentChanges.Count() == 1 && notification.ContentChanges.First().Range == null)
            {
                var change = notification.ContentChanges.First();
                return _bufferManager.UpdateBufferAsync(new OmniSharp.Models.Request()
                {
                    FileName = FromUri(notification.TextDocument.Uri),
                    Buffer = change.Text
                });
            }
            else
            {
                var changes = notification.ContentChanges
                    .Select(change => new LinePositionSpanTextChange()
                    {
                        NewText = change.Text,
                        StartColumn = Convert.ToInt32(change.Range.Start.Character),
                        StartLine = Convert.ToInt32(change.Range.Start.Line),
                        EndColumn = Convert.ToInt32(change.Range.End.Character),
                        EndLine = Convert.ToInt32(change.Range.End.Line),
                    })
                    .ToArray();

                return _bufferManager.UpdateBufferAsync(new OmniSharp.Models.Request()
                {
                    FileName = FromUri(notification.TextDocument.Uri),
                    Changes = changes
                });
            }
        }

        public Task Handle(DidOpenTextDocumentParams notification)
        {
            return _openHandler.Handle(new FileOpenRequest()
            {
                Buffer = notification.TextDocument.Text,
                FileName = FromUri(notification.TextDocument.Uri)
            });
        }

        public Task Handle(DidCloseTextDocumentParams notification)
        {
            return _closeHandler.Handle(new FileCloseRequest()
            {
                FileName = FromUri(notification.TextDocument.Uri)
            });
        }

        public Task Handle(DidSaveTextDocumentParams notification)
        {
            if (_capability?.DidSave == true)
            {
                return _bufferManager.UpdateBufferAsync(new OmniSharp.Models.Request()
                {
                    FileName = FromUri(notification.TextDocument.Uri),
                    Buffer = notification.Text
                });
            }
            return Task.CompletedTask;
        }

        public Task Handle(WillSaveTextDocumentParams notification)
        {
            if (_capability?.WillSave == true)
            {

            }
            return Task.CompletedTask;
        }

        public Task Handle(WillSaveTextDocumentParams request, CancellationToken token)
        {
            if (_capability?.WillSaveWaitUntil == true)
            {

            }
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
