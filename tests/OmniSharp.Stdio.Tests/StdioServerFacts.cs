using System;
using System.Collections.Generic;
using System.Composition.Hosting.Core;
using System.IO;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.Eventing;
using OmniSharp.Protocol;
using OmniSharp.Services;
using Xunit;

namespace OmniSharp.Stdio.Tests
{
    public class StdioServerFacts
    {
        internal static Host BuildTestServerAndStart(TextReader reader, ISharedTextWriter writer, Action<Host> programDelegate = null,
            IEnumerable<ExportDescriptorProvider> additionalExports = null)
        {
            var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
            var environment = new OmniSharpEnvironment();
            var serviceProvider = CompositionHostBuilder.CreateDefaultServiceProvider(environment, configuration, NullEventEmitter.Instance);
            var cancelationTokenSource = new CancellationTokenSource();
            var host = new Host(reader, writer,
                environment,
                serviceProvider,
                new CompositionHostBuilder(serviceProvider, exportDescriptorProviders: additionalExports),
                serviceProvider.GetRequiredService<ILoggerFactory>(),
                cancelationTokenSource);

            programDelegate?.Invoke(host);

            host.Start();

            return host;
        }

        [Fact]
        public void ServerPrintsStartedMessage()
        {
            var writer = new TestTextWriter(
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                }
            );

            using (BuildTestServerAndStart(new StringReader(""), writer))
            {
                Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(10)), "Timeout");
                Assert.Null(writer.Exception);
            }
        }

        [Fact]
        public void ServerRepliesWithErrorToInvalidJson()
        {
            var writer = new TestTextWriter(
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                },
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("error", packet.Event);
                    Assert.NotNull(packet.Body);
                }
            );

            using (BuildTestServerAndStart(new StringReader("notjson\r\n"), writer))
            {
                Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(10)), "Timeout");
                Assert.Null(writer.Exception);
            }
        }

        [Fact]
        public void ServerRepliesWithErrorToInvalidRequest()
        {
            var writer = new TestTextWriter(
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                },
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("error", packet.Event);
                    Assert.NotNull(packet.Body);
                }
            );

            using (BuildTestServerAndStart(new StringReader("{}\r\n"), writer))
            {
                Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(10)), "Timeout");
                Assert.Null(writer.Exception);
            }
        }

        [Fact]
        public void ServerRepliesWithResponse()
        {
            var request = new RequestPacket()
            {
                Seq = 21,
                Command = "foo"
            };

            var writer = new TestTextWriter(
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                },
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<ResponsePacket>(value);
                    Assert.Equal(request.Seq, packet.Request_seq);
                    Assert.Equal(request.Command, packet.Command);
                    Assert.False(packet.Success);
                    Assert.True(packet.Running);
                    Assert.Contains(nameof(NotSupportedException), packet.Message.ToString());
                }
            );

            using (BuildTestServerAndStart(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer))
            {
                Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(1000)), "Timeout");
                Assert.Null(writer.Exception);
            }
        }

        [Fact]
        public void ServerRepliesWithResponseWhenTaskDoesNotReturnAnything()
        {
            var request = new RequestPacket()
            {
                Seq = 21,
                Command = "foo"
            };

            var writer = new TestTextWriter(
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                },
                value =>
                {
                    Assert.Contains("\"Body\":null", value);

                   // Deserialize is too relaxed...
                   var packet = JsonConvert.DeserializeObject<ResponsePacket>(value);
                    Assert.Equal(request.Seq, packet.Request_seq);
                    Assert.Equal(request.Command, packet.Command);
                    Assert.False(packet.Success);
                    Assert.True(packet.Running);
                    Assert.Contains(nameof(NotSupportedException), packet.Message.ToString());
                    Assert.Null(packet.Body);
                }
            );

            using (BuildTestServerAndStart(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer))
            {
                Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(10)), "Timeout");
                Assert.Null(writer.Exception);
            }
        }

        [Fact]
        public void ServerRepliesWithResponseWhenHandlerFails()
        {
            var request = new RequestPacket()
            {
                Seq = 21,
                Command = "foo"
            };

            var writer = new TestTextWriter(
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                },
                value =>
                {
                    var packet = JsonConvert.DeserializeObject<ResponsePacket>(value);
                    Assert.Equal(request.Seq, packet.Request_seq);
                    Assert.Equal(request.Command, packet.Command);
                    Assert.False(packet.Success);
                    Assert.True(packet.Running);
                    Assert.NotNull(packet.Message);
                }
            );

            BuildTestServerAndStart(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer);

            Assert.True(writer.Completion.WaitOne(TimeSpan.FromHours(10)), "Timeout");
            Assert.Null(writer.Exception);
        }
    }
}
