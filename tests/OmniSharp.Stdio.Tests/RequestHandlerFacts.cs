using System.Threading.Tasks;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;
using Microsoft.Framework.OptionsModel;
using OmniSharp.Options;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Transport;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Stdio.Tests
{
    public class RequestHandlerFacts
    {
        [Fact]
        public async Task EventOnInvalidJson()
        {
            var handler = new RequestHandler(null);
            var response = await handler.HandleRequest("farboo");

            Assert.True(response is EventPacket);
            Assert.Equal("error", (response as EventPacket).Event);
        }
     
        [Fact]
        public async Task EventOnInCompleteJson()
        {
            var handler = new RequestHandler(null);
            var response = await handler.HandleRequest("{}") as EventPacket;
            Assert.NotNull(response);
            Assert.Equal("event", response.Type);
            Assert.Equal("error", response.Event);

            response = await handler.HandleRequest(@"{""seq"":1}") as EventPacket;
            Assert.NotNull(response);
            Assert.Equal("event", response.Type);
            Assert.Equal("error", response.Event);
        }

        [Fact]
        public async Task ErrorReplyOnUnknownCommand()
        {
            var handler = new RequestHandler(null);
            var response = await handler.HandleRequest(new RequestPacket(@"{""seq"":1,""command"":""farboo""}").ToString()) as ResponsePacket;

            Assert.NotNull(response);
            Assert.Equal("response", response.Type);
            Assert.Equal(false, response.Success);
            Assert.Equal(true, response.Running);
            Assert.Equal("farboo", response.Command);
            Assert.NotNull(response.Message);
        }

        [Fact]
        public async Task InvokeRoute_checkreadystatus()
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(@"class Far { }", "a.cs");
            var services = new ServiceCollection();
            services.AddInstance(workspace);
            services.AddInstance(typeof(IOptions<OmniSharpOptions>), new OptionsManager<OmniSharpOptions>(new IConfigureOptions<OmniSharpOptions>[]{}));
            services.AddControllers();

            var handler = new RequestHandler(services.BuildServiceProvider());
            var response = await handler.HandleRequest(new RequestPacket(@"{""type"":""request"",""seq"":13,""command"":""checkreadystatus""}").ToString()) as ResponsePacket;

            Assert.NotNull(response);
            Assert.Null(response.Message);
            Assert.Equal(true, response.Running);
            Assert.Equal(true, response.Success);
            Assert.Equal(13, response.Request_seq);
            Assert.Equal("checkreadystatus", response.Command);
            Assert.IsType(typeof(bool), response.Body);
        }
        
        [Fact]
        public async Task ArgumentsCallbackIsCalled()
        {
            var workspace = TestHelpers.CreateSimpleWorkspace(@"class Far { }", "a.cs");
            var services = new ServiceCollection();
            services.AddInstance(workspace);
            services.AddInstance(typeof(IOptions<OmniSharpOptions>), new OptionsManager<OmniSharpOptions>(new IConfigureOptions<OmniSharpOptions>[]{}));
            services.AddControllers();

            var called = false;
            var handler = new RequestHandler(services.BuildServiceProvider(), arg =>
            {
                called = true;
                return arg;
            });
            var response = await handler.HandleRequest(new RequestPacket(@"{""type"":""request"",""seq"":13,""command"":""typelookup"",""arguments"":{}}").ToString()) as ResponsePacket;

            Assert.True(called);
        }
    }
}