using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.Endpoint;
using OmniSharp.Eventing;
using OmniSharp.Services;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;
using Xunit;

namespace OmniSharp.Stdio.Tests
{
    public class StdioServerFacts
    {
        private Host BuildTestServerAndStart(TextReader reader, ISharedTextWriter writer, Action<Host> programDelegate = null)
        {
            var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
            var serviceProvider = MefBuilder.CreateDefaultServiceProvider(configuration);
            var omniSharpEnvironment = new OmniSharpEnvironment();
            var cancelationTokenSource = new CancellationTokenSource();
            var server = new Host(reader, writer,
                omniSharpEnvironment,
                configuration,
                serviceProvider,
                new MefBuilder(serviceProvider, omniSharpEnvironment, writer, NullEventEmitter.Instance).Build(Enumerable.Empty<Assembly>()),
                serviceProvider.GetRequiredService<ILoggerFactory>(),
                cancelationTokenSource);

            programDelegate?.Invoke(server);

            server.Start();

            return server;
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

        //[Fact( Skip = "unable to send request properly")]
        //public void ServerRepliesWithResponse()
        //{
        //    var request = new RequestPacket()
        //    {
        //        Seq = 21,
        //        Command = "foo"
        //    };

        //    var writer = new TestTextWriter(
        //        value =>
        //        {
        //            var packet = JsonConvert.DeserializeObject<EventPacket>(value);
        //            Assert.Equal("started", packet.Event);
        //        },
        //        value =>
        //        {
        //            var packet = JsonConvert.DeserializeObject<ResponsePacket>(value);
        //            Assert.Equal(request.Seq, packet.Request_seq);
        //            Assert.Equal(request.Command, packet.Command);
        //            Assert.True(packet.Success);
        //            Assert.True(packet.Running);
        //            Assert.Null(packet.Message);
        //        }
        //    );

        //    using (BuildTestServerAndStart(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer))
        //    {
        //        Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(1000)), "Timeout");
        //        Assert.Null(writer.Exception);
        //    }
        //}

        //[Fact]
        //public void ServerRepliesWithResponseWhenTaskDoesNotReturnAnything()
        //{
        //    var request = new RequestPacket()
        //    {
        //        Seq = 21,
        //        Command = "foo"
        //    };

        //    var writer = new TestTextWriter(
        //        value =>
        //        {
        //            var packet = JsonConvert.DeserializeObject<EventPacket>(value);
        //            Assert.Equal("started", packet.Event);
        //        },
        //        value =>
        //        {
        //            Assert.Contains("\"Body\":null", value);

        //            // Deserialize is too relaxed...
        //            var packet = JsonConvert.DeserializeObject<ResponsePacket>(value);
        //            Assert.Equal(request.Seq, packet.Request_seq);
        //            Assert.Equal(request.Command, packet.Command);
        //            Assert.True(packet.Success);
        //            Assert.True(packet.Running);
        //            Assert.Null(packet.Message);
        //            Assert.Null(packet.Body);
        //        }
        //    );

        //    using (BuildTestServerAndStart(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer))
        //    {
        //        Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(10)), "Timeout");
        //        Assert.Null(writer.Exception);
        //    }

        //}

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
