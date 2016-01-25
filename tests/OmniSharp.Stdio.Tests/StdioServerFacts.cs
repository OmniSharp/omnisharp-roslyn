using System;
using System.IO;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;
using Xunit;

namespace OmniSharp.Stdio.Tests
{
    public class StdioServerFacts
    {
        private IServer BuildTestServerAndStart(TextReader reader,
                                                ISharedTextWriter writer,
                                                IHttpApplication<int> application)
        {
            var factory = new StdioServerFactory(reader, writer);
            var server = factory.CreateServer(new ConfigurationBuilder().Build());
            server.Start(application);

            return server;
        }

        private IServer BuildTestServerAndStart(TextReader reader, ISharedTextWriter writer)
        {
            return BuildTestServerAndStart(reader, writer, new MockHttpApplication());
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

            BuildTestServerAndStart(new StringReader(""), writer);

            Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(10)), "Timeout");
            Assert.Null(writer.Exception);
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

            BuildTestServerAndStart(new StringReader("notjson\r\n"), writer);
            Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(10)), "Timeout");
            Assert.Null(writer.Exception);
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

            BuildTestServerAndStart(new StringReader("{}\r\n"), writer);
            Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(10)), "Timeout");
            Assert.Null(writer.Exception);
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
                    Assert.Equal(true, packet.Success);
                    Assert.Equal(true, packet.Running);
                    Assert.Null(packet.Message);
                }
            );

            BuildTestServerAndStart(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer);
            Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(1000)), "Timeout");
            Assert.Null(writer.Exception);
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
                    Assert.True(value.Contains("\"Body\":null"));

                    // Deserialize is too relaxed...
                    var packet = JsonConvert.DeserializeObject<ResponsePacket>(value);
                    Assert.Equal(request.Seq, packet.Request_seq);
                    Assert.Equal(request.Command, packet.Command);
                    Assert.Equal(true, packet.Success);
                    Assert.Equal(true, packet.Running);
                    Assert.Null(packet.Message);
                    Assert.Null(packet.Body);
                }
            );

            BuildTestServerAndStart(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer);
            Assert.True(writer.Completion.WaitOne(TimeSpan.FromSeconds(10)), "Timeout");
            Assert.Null(writer.Exception);
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
                    Assert.Equal(false, packet.Success);
                    Assert.Equal(true, packet.Running);
                    Assert.NotNull(packet.Message);
                }
            );

            var exceptionApplication = new MockHttpApplication
            {
                ProcessAction = _ => { throw new Exception("Boom"); }
            };

            BuildTestServerAndStart(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer, exceptionApplication);

            Assert.True(writer.Completion.WaitOne(TimeSpan.FromHours(10)), "Timeout");
            Assert.Null(writer.Exception);
        }
    }
}