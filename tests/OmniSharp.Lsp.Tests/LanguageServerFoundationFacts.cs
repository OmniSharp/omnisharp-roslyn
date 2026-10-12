using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.LanguageProtocol.Testing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Options;
using TestUtility;
using Xunit;

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

        /// <summary>
        /// This ensures the server has registered all handlers
        /// </summary>
        [Fact]
        public void Registers_all_known_OmniSharp_handlers()
        {
            var descriptors = Server.GetRequiredService<IHandlersManager>().Descriptors;

            foreach (var method in RegistersAllKnownOmniSharpHandlersData.Methods)
            {
                var descriptor = descriptors.FirstOrDefault(z => z.Method == $"o#{method}".ToLowerInvariant());
                Assert.True(descriptor is not null, $"Expected handler '{method}' to be registered.");
            }
        }

        /// <summary>
        /// This ensures that the client can call the methods (the server has properly registered it)
        /// </summary>
        /// <param name="methods"></param>
        /// <returns></returns>
        [Theory]
        [ClassData(typeof(CallableOmniSharpHandlerBatchesData))]
        public async Task All_known_OmniSharp_handlers_are_callable(string[] methods)
        {
            foreach (var method in methods)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(1));
                    await Client.SendRequest($"o#{method}".ToLowerInvariant(), new object()).ReturningVoid(cts.Token);
                }
                catch (MethodNotSupportedException)
                {
                    Assert.Fail($"Method '{method}' should be supported.");
                }
                catch (Exception e)
                {
                    Logger.LogWarning(e, "Got exception calling {Method}", method);
                }
            }
        }

        private static class RegistersAllKnownOmniSharpHandlersData
        {
            public static IReadOnlyList<string> Methods { get; } = GetMethods();

            private static IReadOnlyList<string> GetMethods()
            {
                var methods = new List<string>();
                var v1Fields = typeof(OmniSharpEndpoints).GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(z => z.FieldType == typeof(string));
                foreach (var field in v1Fields)
                {
                    methods.Add(field.GetValue(null) as string);
                }

                var v2Fields = typeof(OmniSharpEndpoints.V2).GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(z => z.FieldType == typeof(string));
                foreach (var field in v2Fields)
                {
                    methods.Add(field.GetValue(null) as string);
                }

                return methods;
            }
        }

        private sealed class CallableOmniSharpHandlerBatchesData : TheoryData<string[]>
        {
            private const int BatchSize = 9;

            public CallableOmniSharpHandlerBatchesData()
            {
                foreach (var batch in RegistersAllKnownOmniSharpHandlersData.Methods.Chunk(BatchSize))
                {
                    Add(batch);
                }
            }
        }
    }
}
