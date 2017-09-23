using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.Endpoint;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Mef;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Plugins;
using OmniSharp.Services;
using OmniSharp.LanguageServerProtocol.Logging;
using OmniSharp.Utilities;
using OmniSharp.Stdio.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Abstractions;
using OmniSharp.Extensions.LanguageServer.Capabilities.Client;
using OmniSharp.Extensions.LanguageServer.Capabilities.Server;
using OmniSharp.Extensions.LanguageServer.Models;
using OmniSharp.Models.FileOpen;
using OmniSharp.Models.FileClose;
using OmniSharp.Roslyn;
using OmniSharp.Models.ChangeBuffer;
using OmniSharp.Models;
using System.Composition;

namespace OmniSharp.LanguageServerProtocol
{
    class LanguageServerHost : IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly Stream _input;
        private readonly Stream _output;
        private readonly LanguageServer _server;
        private readonly ISharedTextWriter _writer;
        private readonly IServiceProvider _serviceProvider;
        private readonly CompositionHost _compositionHost;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOmniSharpEnvironment _environment;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CachedStringBuilder _cachedStringBuilder;

        public LanguageServerHost(
            Stream input, Stream output, IOmniSharpEnvironment environment, IConfiguration configuration,
            IServiceProvider serviceProvider, CompositionHostBuilder compositionHostBuilder, ILoggerFactory loggerFactory, CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _input = input;
            _output = output;

            _server = new LanguageServer(_input, _output);
            // _writer = new SharedTextWriter(output);
            _environment = environment;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _loggerFactory = loggerFactory.AddLanguageServer(_server, (category, level) => HostHelpers.LogFilter(category, level, _environment));

            _compositionHost = compositionHostBuilder.Build();
            _cachedStringBuilder = new CachedStringBuilder();
        }
        public void Dispose()
        {
            _compositionHost?.Dispose();
            _loggerFactory?.Dispose();
            _cancellationTokenSource?.Dispose();
        }

        public async Task Start()
        {
            _server.AddHandler(_compositionHost.GetExport<TextDocumentSyncHandler>());

            await _server.Initialize();

            var logger = _loggerFactory.CreateLogger<LanguageServerHost>();

            WorkspaceInitializer.Initialize(_serviceProvider, _compositionHost, _configuration, logger);

            logger.LogInformation($"Omnisharp server running using Lsp at location '{_environment.TargetDirectory}' on host {_environment.HostProcessId}.");

            Console.CancelKeyPress += (sender, e) =>
            {
                _cancellationTokenSource.Cancel();
                e.Cancel = true;
            };

            if (_environment.HostProcessId != -1)
            {
                try
                {
                    var hostProcess = Process.GetProcessById(_environment.HostProcessId);
                    hostProcess.EnableRaisingEvents = true;
                    hostProcess.OnExit(() => _cancellationTokenSource.Cancel());
                }
                catch
                {
                    // If the process dies before we get here then request shutdown
                    // immediately
                    _cancellationTokenSource.Cancel();
                }
            }
        }
    }

    [Shared]
    [Export(typeof(TextDocumentSyncHandler))]
    [Export(typeof(ITextDocumentSyncHandler))]
    class TextDocumentSyncHandler : ITextDocumentSyncHandler
    {
        // TODO Make this configurable?
        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.cs",
                Language = "csharp",
            }
        );
        private SynchronizationCapability _capability;
        private readonly IRequestHandler<FileOpenRequest, FileOpenResponse> _openHandler;
        private readonly IRequestHandler<FileCloseRequest, FileCloseResponse> _closeHandler;
        private readonly BufferManager _bufferManager;
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public TextDocumentSyncHandler(
            IRequestHandler<FileOpenRequest, FileOpenResponse> openHandler,
            IRequestHandler<FileCloseRequest, FileCloseResponse> closeHandler,
            BufferManager bufferManager,
            OmniSharpWorkspace workspace
            )
        {
            _openHandler = openHandler;
            _closeHandler = closeHandler;
            _bufferManager = bufferManager;
            _workspace = workspace;
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
            var document = _workspace.GetDocument(uri.LocalPath);
            if (document == null) return new TextDocumentAttributes(uri, "");
            return new TextDocumentAttributes(uri, "csharp");
        }

        public Task Handle(DidChangeTextDocumentParams notification)
        {
            var changes = notification.ContentChanges
                .Select(change => new LinePositionSpanTextChange()
                {
                    NewText = change.Text,
                    StartColumn = Convert.ToInt32(change.Range.Start.Character),
                    StartLine = Convert.ToInt32(change.Range.Start.Line),
                    EndColumn = Convert.ToInt32(change.Range.End.Character),
                    EndLine = Convert.ToInt32(change.Range.End.Line),
                });

            return _bufferManager.UpdateBufferAsync(new OmniSharp.Models.Request()
            {
                FileName = notification.TextDocument.Uri.LocalPath,
                Changes = changes
            });
        }

        public Task Handle(DidOpenTextDocumentParams notification)
        {
            return _openHandler.Handle(new FileOpenRequest()
            {
                Buffer = notification.TextDocument.Text,
                FileName = notification.TextDocument.Uri.LocalPath
            });
        }

        public Task Handle(DidCloseTextDocumentParams notification)
        {
            return _closeHandler.Handle(new FileCloseRequest()
            {
                FileName = notification.TextDocument.Uri.LocalPath
            });
        }

        public Task Handle(DidSaveTextDocumentParams notification)
        {
            return _bufferManager.UpdateBufferAsync(new OmniSharp.Models.Request()
            {
                FileName = notification.TextDocument.Uri.LocalPath,
                Buffer = notification.Text
            });
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
