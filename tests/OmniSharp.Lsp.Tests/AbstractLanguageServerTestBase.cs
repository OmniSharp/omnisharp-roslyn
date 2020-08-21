using System;
using System.Collections.Generic;
using System.Composition.Hosting.Core;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc.Testing;
using OmniSharp.Extensions.LanguageProtocol.Testing;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.LanguageServerProtocol;
using OmniSharp.Models;
using OmniSharp.Models.FilesChanged;
using OmniSharp.Models.UpdateBuffer;
using TestUtility;
using TestUtility.Logging;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Lsp.Tests
{
    public abstract class AbstractLanguageServerTestBase : LanguageServerTestBase, IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        private readonly ILoggerFactory _loggerFactory;
        private LanguageServerHost _host;
        private Task startUpTask;
        private IConfiguration _setupConfiguration;
        protected ILogger Logger { get; }

        protected AbstractLanguageServerTestBase(ITestOutputHelper output, IConfiguration configuration = null) : this(output,
            new LoggerFactory().AddXunit(output))
        {
            _setupConfiguration = configuration;
        }

        private AbstractLanguageServerTestBase(ITestOutputHelper output, ILoggerFactory loggerFactory, IConfiguration configuration = null) : base(
            new JsonRpcTestOptions()
                .WithClientLoggerFactory(loggerFactory)
                .WithServerLoggerFactory(loggerFactory)
        )
        {
            _output = output;
            _loggerFactory = loggerFactory;
            Logger = _loggerFactory.CreateLogger(GetType());
            _setupConfiguration = configuration;
        }

        protected override (Stream clientOutput, Stream serverInput) SetupServer()
        {
            var clientPipe = new Pipe(TestOptions.DefaultPipeOptions);
            var serverPipe = new Pipe(TestOptions.DefaultPipeOptions);

            _host = new LanguageServerHost(
                clientPipe.Reader.AsStream(),
                serverPipe.Writer.AsStream(),
                options => options
                    .ConfigureLogging(x => x.AddLanguageProtocolLogging())
                    .WithServices(services => { services.AddSingleton(_loggerFactory); })
                    .OnInitialize((server, request, token) =>
                    {
                        var configBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                            .AddConfiguration(server.Configuration.GetSection("csharp"))
                            .AddConfiguration(server.Configuration.GetSection("omnisharp"));
                        if (_setupConfiguration != null) configBuilder.AddConfiguration(_setupConfiguration);
                        var config =  configBuilder.Build();
                        OmniSharpTestHost = CreateOmniSharpHost(config);
                        var handlers =
                            LanguageServerHost.ConfigureCompositionHost(server, OmniSharpTestHost.CompositionHost);
                        _host.UnderTest(OmniSharpTestHost.ServiceProvider, OmniSharpTestHost.CompositionHost);
                        LanguageServerHost.RegisterHandlers(server, OmniSharpTestHost.CompositionHost, handlers);
                        return Task.CompletedTask;
                    }),
                CancellationTokenSource.CreateLinkedTokenSource(CancellationToken)
            );
            startUpTask = _host.Start();

            return (serverPipe.Reader.AsStream(), clientPipe.Writer.AsStream());
        }

        public async Task Restart(IConfiguration configuration = null, IDictionary<string, string> configurationData = null)
        {
            _host.Dispose();
            Disposable.Remove(Client);
            Client.Dispose();
            OmniSharpTestHost.Dispose();

            _setupConfiguration = configuration ?? new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(configurationData ?? new Dictionary<string, string>())
                .Build();
            await InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            var(client, configurationProvider) = await InitializeClientWithConfiguration(x =>
            {
                x.WithCapability(new WorkspaceEditCapability()
                {
                    DocumentChanges = true,
                    FailureHandling = FailureHandlingKind.Undo,
                    ResourceOperations = new[]
                    {
                        ResourceOperationKind.Create, ResourceOperationKind.Delete, ResourceOperationKind.Rename
                    }
                });
                x.OnApplyWorkspaceEdit(async @params =>
                {
                    if (@params.Edit?.Changes != null)
                    {
                        foreach (var change in @params.Edit.Changes)
                        {
                            var changes = change.Value
                                .Select(change => new LinePositionSpanTextChange()
                                {
                                    NewText = change.NewText,
                                    StartColumn = Convert.ToInt32(change.Range.Start.Character),
                                    StartLine = Convert.ToInt32(change.Range.Start.Line),
                                    EndColumn = Convert.ToInt32(change.Range.End.Character),
                                    EndLine = Convert.ToInt32(change.Range.End.Line),
                                })
                                .ToArray();

                            await OmniSharpTestHost.Workspace.BufferManager.UpdateBufferAsync(new UpdateBufferRequest()
                            {
                                FileName = LanguageServerProtocol.Helpers.FromUri(change.Key),
                                Changes = changes
                            });
                        }
                    }
                    else if (@params.Edit?.DocumentChanges != null)
                    {
                        foreach (var change in @params.Edit.DocumentChanges)
                        {
                            if (change.IsTextDocumentEdit)
                            {
                                var contentChanges = change.TextDocumentEdit.Edits.ToArray();
                                if (contentChanges.Length == 1 && contentChanges[0].Range == null)
                                {
                                    var c = contentChanges[0];
                                    await OmniSharpTestHost.Workspace.BufferManager.UpdateBufferAsync(
                                        new UpdateBufferRequest()
                                        {
                                            FileName = LanguageServerProtocol.Helpers.FromUri(change.TextDocumentEdit
                                                .TextDocument.Uri),
                                            Buffer = c.NewText
                                        });
                                }
                                else
                                {
                                    var changes = contentChanges
                                        .Select(change => new LinePositionSpanTextChange()
                                        {
                                            NewText = change.NewText,
                                            StartColumn = Convert.ToInt32(change.Range.Start.Character),
                                            StartLine = Convert.ToInt32(change.Range.Start.Line),
                                            EndColumn = Convert.ToInt32(change.Range.End.Character),
                                            EndLine = Convert.ToInt32(change.Range.End.Line),
                                        })
                                        .ToArray();

                                    await OmniSharpTestHost.Workspace.BufferManager.UpdateBufferAsync(
                                        new UpdateBufferRequest()
                                        {
                                            FileName = LanguageServerProtocol.Helpers.FromUri(change.TextDocumentEdit
                                                .TextDocument.Uri),
                                            Changes = changes
                                        });
                                }
                            }

                            if (change.IsRenameFile)
                            {
                                var documents =
                                    OmniSharpTestHost.Workspace.GetDocuments(
                                        change.RenameFile.OldUri.GetFileSystemPath());
                                foreach (var oldDocument in documents)
                                {
                                    var text = await oldDocument.GetTextAsync();
                                    var newFilePath = change.RenameFile.NewUri.GetFileSystemPath();
                                    var newFileName = Path.GetFileName(newFilePath);
                                    OmniSharpTestHost.Workspace.TryApplyChanges(
                                        OmniSharpTestHost.Workspace.CurrentSolution
                                            .RemoveDocument(oldDocument.Id)
                                            .AddDocument(
                                                DocumentId.CreateNewId(oldDocument.Project.Id, newFileName),
                                                newFileName,
                                                text,
                                                oldDocument.Folders,
                                                newFilePath
                                            )
                                    );
                                }
                            }
                        }
                    }

                    await ClientEvents.SettleNext();

                    return new ApplyWorkspaceEditResponse()
                    {
                        Applied = true
                    };
                });
            });
            Client = client;

            await startUpTask;
            Configuration = new ConfigurationProvider(Server, Client, configurationProvider, CancellationToken);
        }

        public Task DisposeAsync()
        {
            _host.Dispose();
            OmniSharpTestHost.Dispose();
            return Task.CompletedTask;
        }

        protected async Task<Project> AddProjectToWorkspace(ITestProject testProject)
        {
            var projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                testProject.Name,
                testProject.Name,
                LanguageNames.CSharp,
                Directory.EnumerateFiles(testProject.Directory, "*.csproj", SearchOption.TopDirectoryOnly).Single()
            );
            OmniSharpTestHost.Workspace.AddProject(projectInfo);
            await OmniSharpTestHost.RestoreProject(testProject);
            await OmniSharpTestHost.GetFilesChangedService().Handle(Directory.GetFiles(testProject.Directory)
                .Select(file => new FilesChangedRequest() {FileName = file, ChangeType = FileWatching.FileChangeType.Create}));
            var project = OmniSharpTestHost.Workspace.CurrentSolution.GetProject(projectInfo.Id);
            return project;
        }

        protected OmniSharpTestHost OmniSharpTestHost { get; private set; }
        protected ConfigurationProvider Configuration { get; private set; }
        protected ILanguageClient Client { get; private set; }
        protected ILanguageServer Server => _host.Server;

        protected OmniSharpTestHost CreateOmniSharpHost(
            IConfiguration configurationData,
            string path = null,
            DotNetCliVersion dotNetCliVersion = DotNetCliVersion.Current,
            IEnumerable<ExportDescriptorProvider> additionalExports = null)
            => OmniSharpTestHost.Create(path, this._output, configurationData, dotNetCliVersion, additionalExports);
    }
}
