using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageProtocol.Testing;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace OmniSharp.Lsp.Tests
{
    public class ConfigurationProvider
    {
        private readonly ILanguageServer _server;
        private readonly ILanguageClient _client;
        private readonly TestConfigurationProvider _configurationProvider;
        private readonly CancellationToken _cancellationToken;

        public ConfigurationProvider(
            ILanguageServer server,
            ILanguageClient client,
            TestConfigurationProvider configurationProvider,
            CancellationToken cancellationToken)
        {
            _server = server;
            _client = client;
            _configurationProvider = configurationProvider;
            _cancellationToken = cancellationToken;
        }

        public Task Update(string section, IDictionary<string, string> configuration)
        {
            if (configuration == null) return Task.CompletedTask;
            return Update(section, new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
        }

        public Task Update(string section, IConfiguration configuration)
        {
            if (configuration == null) return Task.CompletedTask;
            return Update(section, null, configuration);
        }

        public Task Update(string section, DocumentUri documentUri, IDictionary<string, string> configuration)
        {
            if (configuration == null) return Task.CompletedTask;
            return Update(section, documentUri, new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
        }

        public Task Update(string section, DocumentUri documentUri, IConfiguration configuration)
        {
            if (configuration == null) return Task.CompletedTask;
            _configurationProvider.Update(section, documentUri, configuration);
            return TriggerChange();
        }

        public Task Reset(string section)
        {
            return Reset(section, null);
        }

        public Task Reset(string section, DocumentUri documentUri)
        {
            _configurationProvider.Reset(section, documentUri);
            return TriggerChange();
        }

        private async Task TriggerChange()
        {
            await _server.Configuration.WaitForChange(_cancellationToken);
        }
    }
}
