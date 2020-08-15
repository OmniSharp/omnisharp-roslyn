using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageProtocol.Testing;
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

            var originalIndentationSize = options.CurrentValue.FormattingOptions.IndentationSize;

            await Task.WhenAll(
                Configuration.Update("csharp", new Dictionary<string, string>()
                {
                    ["FormattingOptions:IndentationSize"] = "12",
                }),
                Configuration.Update("omnisharp", new Dictionary<string, string>()
                {
                    ["RenameOptions:RenameOverloads"] = "true",
                }),
                options.WaitForChange(CancellationToken)
            );

            Assert.NotEqual(originalIndentationSize, options.CurrentValue.FormattingOptions.IndentationSize);
            Assert.Equal(12, options.CurrentValue.FormattingOptions.IndentationSize);
            Assert.True(options.CurrentValue.RenameOptions.RenameOverloads);
        }
    }
}
