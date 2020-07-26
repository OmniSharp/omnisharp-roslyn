using System;
using System.Collections.Generic;
using System.Composition.Hosting.Core;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc.Testing;
using OmniSharp.Extensions.LanguageProtocol.Testing;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.LanguageServerProtocol;
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
        protected ILogger Logger { get; }

        protected AbstractLanguageServerTestBase(ITestOutputHelper output) : this(output, new LoggerFactory().AddXunit(output))
        {
        }

        private AbstractLanguageServerTestBase(ITestOutputHelper output, ILoggerFactory loggerFactory) : base(
            new JsonRpcTestOptions()
                .WithClientLoggerFactory(loggerFactory)
                .WithServerLoggerFactory(loggerFactory)
        )
        {
            _output = output;
            _loggerFactory = loggerFactory;
            Logger = _loggerFactory.CreateLogger(GetType());
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
                    .WithServices(services =>
                    {
                        services.AddSingleton(_loggerFactory);
                    })
                    .OnInitialize((server, request, token) =>
                    {
                        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                            .AddConfiguration(server.Configuration.GetSection("csharp"))
                            .AddConfiguration(server.Configuration.GetSection("omnisharp"))
                            .Build();
                        OmniSharpTestHost = CreateOmniSharpHost(config);
                        var handlers = LanguageServerHost.ConfigureCompositionHost(server, OmniSharpTestHost.CompositionHost);
                        _host.UnderTest(OmniSharpTestHost.ServiceProvider, OmniSharpTestHost.CompositionHost);
                        LanguageServerHost.RegisterHandlers(server, OmniSharpTestHost.CompositionHost, handlers);
                        return Task.CompletedTask;
                    }),
                CancellationTokenSource.CreateLinkedTokenSource(CancellationToken)
            );
            startUpTask = _host.Start();

            return (serverPipe.Reader.AsStream(), clientPipe.Writer.AsStream());
        }

        public async Task InitializeAsync()
        {
            Client = await InitializeClient(x => { });
            await startUpTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _host.Dispose();
        }

        protected OmniSharpTestHost OmniSharpTestHost { get; private set; }
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
