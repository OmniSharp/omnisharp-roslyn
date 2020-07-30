using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Options;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Lsp.Tests
{
    public class LanguageServerFoundationFacts : AbstractLanguageServerTestBase
    {
        public LanguageServerFoundationFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task Language_server_contributes_configuration_from_client()
        {
            var options = OmniSharpTestHost.ServiceProvider.GetRequiredService<IOptionsMonitor<OmniSharpOptions>>();


            using (Client.Register(x => x.OnConfiguration(request =>
            {
                return Task.FromResult(new Container<JToken>(Enumerable.Select<ConfigurationItem, JObject>(request.Items, item =>
                    item.Section == "csharp" ? new JObject()
                    {
                        ["FormattingOptions"] = new JObject() {["IndentationSize"] = 12,}
                    } :
                    item.Section == "omnisharp" ? new JObject()
                    {
                        ["RenameOptions"] = new JObject() {["RenameOverloads"] = true,}
                    } : new JObject())));
            })))
            {
                ChangeToken.OnChange(Server.Configuration.GetReloadToken,
                    () => { Logger?.LogCritical("Server Reloaded!"); });

                ChangeToken.OnChange(
                    OmniSharpTestHost.ServiceProvider.GetRequiredService<IConfiguration>().GetReloadToken,
                    () => { Logger?.LogCritical("Host Reloaded!"); });

                var originalIndentationSize = options.CurrentValue.FormattingOptions.IndentationSize;

                Client.Workspace.DidChangeConfiguration(new DidChangeConfigurationParams() {Settings = null});

                await options.WaitForChange(CancellationToken);

                Assert.NotEqual(originalIndentationSize, options.CurrentValue.FormattingOptions.IndentationSize);
                Assert.Equal(12, options.CurrentValue.FormattingOptions.IndentationSize);
                Assert.True(options.CurrentValue.RenameOptions.RenameOverloads);
            }
        }
    }
}
