using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OmniSharp.Stdio.Protocol;
using Xunit;

namespace OmniSharp.Stdio.Tests
{
    public class StdioServerFacts
    {
        [Fact]
        public async Task ServerPrintsStartedMessage()
        {
            var writer = new TestTextWriter(new Action<string>[] {
                value => {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                }
            });

            var factory = new StdioServerFactory(new StringReader(""), writer);
            factory.Start(new StdioServerInformation(), features => Task.FromResult<object>(null));

            await writer.Completion;
        }

        [Fact]
        public async Task ServerRepliesWithErrorToInvalidJson()
        {
            var writer = new TestTextWriter(new Action<string>[] {
                value => {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                },
                value => {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("error", packet.Event);
                    Assert.NotNull(packet.Body);
                }
            });

            var factory = new StdioServerFactory(new StringReader("notjson\r\n"), writer);
            factory.Start(new StdioServerInformation(), features => Task.FromResult<object>(null));

            await writer.Completion;
        }

        [Fact]
        public async Task ServerRepliesWithErrorToInvalidRequest()
        {
            var writer = new TestTextWriter(new Action<string>[] {
                value => {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                },
                value => {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("error", packet.Event);
                    Assert.NotNull(packet.Body);
                }
            });

            var factory = new StdioServerFactory(new StringReader("{}\r\n"), writer);
            factory.Start(new StdioServerInformation(), features => Task.FromResult<object>(null));

            await writer.Completion;
        }

        [Fact]
        public async Task ServerRepliesWithResponse()
        {
            var request = new RequestPacket()
            {
                Seq = 21,
                Command = "foo"
            };

            var writer = new TestTextWriter(new Action<string>[] {
                value => {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                },
                value => {
                    var packet = JsonConvert.DeserializeObject<ResponsePacket>(value);
                    Assert.Equal(request.Seq, packet.Request_seq);
                    Assert.Equal(request.Command, packet.Command);
                    Assert.Equal(true, packet.Success);
                    Assert.Equal(true, packet.Running);
                    Assert.Null(packet.Message);
                }
            });

            var factory = new StdioServerFactory(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer);
            factory.Start(new StdioServerInformation(), features =>
            {
                return Task.FromResult<object>(null);
            });

            await writer.Completion;
        }
        
        [Fact]
        public async Task ServerRepliesWithResponseWhenTaskDoesNotReturnAnything()
        {
            var request = new RequestPacket()
            {
                Seq = 21,
                Command = "foo"
            };

            var writer = new TestTextWriter(new Action<string>[] {
                value => {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                },
                value => {
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
            });

            var factory = new StdioServerFactory(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer);
            factory.Start(new StdioServerInformation(), features =>
            {
                return Task.WhenAll();
            });

            await writer.Completion;
        }

        [Fact]
        public async Task ServerRepliesWithResponseWhenHandlerFails()
        {
            var request = new RequestPacket()
            {
                Seq = 21,
                Command = "foo"
            };

            var writer = new TestTextWriter(new Action<string>[] {
                value => {
                    var packet = JsonConvert.DeserializeObject<EventPacket>(value);
                    Assert.Equal("started", packet.Event);
                },
                value => {
                    var packet = JsonConvert.DeserializeObject<ResponsePacket>(value);
                    Assert.Equal(request.Seq, packet.Request_seq);
                    Assert.Equal(request.Command, packet.Command);
                    Assert.Equal(false, packet.Success);
                    Assert.Equal(true, packet.Running);
                    Assert.NotNull(packet.Message);
                }
            });

            var factory = new StdioServerFactory(new StringReader(JsonConvert.SerializeObject(request) + "\r\n"), writer);
            factory.Start(new StdioServerInformation(), features =>
            {
                throw new Exception();
            });

            await writer.Completion;
        }
    }
}
