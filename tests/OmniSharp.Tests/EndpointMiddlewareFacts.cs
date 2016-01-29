using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.Mef;
using OmniSharp.Middleware;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using OmniSharp.Services;
using Xunit;

namespace OmniSharp.Tests
{
    public class EndpointMiddlewareFacts
    {
        [OmniSharpHandler(OmnisharpEndpoints.GotoDefinition, LanguageNames.CSharp)]
        public class GotoDefinitionService : RequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>
        {
            [Import]
            public OmnisharpWorkspace Workspace { get; set; }

            public Task<GotoDefinitionResponse> Handle(GotoDefinitionRequest request)
            {
                return Task.FromResult<GotoDefinitionResponse>(null);
            }
        }

        [OmniSharpHandler(OmnisharpEndpoints.FindSymbols, LanguageNames.CSharp)]
        public class FindSymbolsService : RequestHandler<FindSymbolsRequest, QuickFixResponse>
        {
            [Import]
            public OmnisharpWorkspace Workspace { get; set; }

            public Task<QuickFixResponse> Handle(FindSymbolsRequest request)
            {
                return Task.FromResult<QuickFixResponse>(null);
            }
        }

        [OmniSharpHandler(OmnisharpEndpoints.UpdateBuffer, LanguageNames.CSharp)]
        public class UpdateBufferService : RequestHandler<UpdateBufferRequest, object>
        {
            [Import]
            public OmnisharpWorkspace Workspace { get; set; }

            public Task<object> Handle(UpdateBufferRequest request)
            {
                return Task.FromResult<object>(null);
            }
        }

        class Response { }

        [Export(typeof(IProjectSystem))]
        class FakeProjectSystem : IProjectSystem
        {
            public string Key { get { return "Fake"; } }
            public string Language { get { return LanguageNames.CSharp; } }
            public IEnumerable<string> Extensions { get; } = new[] { ".cs" };

            public Task<object> GetInformationModel(WorkspaceInformationRequest request)
            {
                throw new NotImplementedException();
            }

            public Task<object> GetProjectModel(string path)
            {
                throw new NotImplementedException();
            }

            public void Initalize(IConfiguration configuration) { }
        }

        class LoggerFactory : ILoggerFactory
        {
            public LogLevel MinimumLevel { get; set; }
            public void AddProvider(ILoggerProvider provider) { }
            public ILogger CreateLogger(string categoryName) => new Logger();
            public void Dispose() { }
        }

        class Disposable : IDisposable { public void Dispose() { } }

        class Logger : ILogger
        {
            public IDisposable BeginScopeImpl(object state) => new Disposable();
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) { }
        }

        [Fact]
        public async Task Passes_through_for_invalid_path()
        {
            RequestDelegate _next = (ctx) => Task.Run(() => { throw new NotImplementedException(); });

            var host = TestHelpers.CreatePluginHost(new[] { typeof(EndpointMiddlewareFacts).GetTypeInfo().Assembly });
            var middleware = new EndpointMiddleware(_next, host, new LoggerFactory());

            var context = new DefaultHttpContext();
            context.Request.Path = PathString.FromUriComponent("/notvalid");

            await Assert.ThrowsAsync<NotImplementedException>(() => middleware.Invoke(context));
        }

        [Fact]
        public async Task Does_not_throw_for_valid_path()
        {
            RequestDelegate _next = (ctx) => Task.Run(() => { throw new NotImplementedException(); });

            var host = TestHelpers.CreatePluginHost(new[] { typeof(EndpointMiddlewareFacts).GetTypeInfo().Assembly, typeof(EndpointDescriptor).GetTypeInfo().Assembly });
            var middleware = new EndpointMiddleware(_next, host, new LoggerFactory());

            var context = new DefaultHttpContext();
            context.Request.Path = PathString.FromUriComponent("/gotodefinition");

            var memoryStream = new MemoryStream();

            context.Request.Body = new MemoryStream(
                Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new GotoDefinitionRequest
                    {
                        FileName = "bar.cs",
                        Line = 2,
                        Column = 14,
                        Timeout = 60000
                    })
                )
            );

            await middleware.Invoke(context);

            Assert.True(true);
        }

        [Fact]
        public async Task Passes_through_to_services()
        {
            RequestDelegate _next = (ctx) => Task.Run(() => { throw new NotImplementedException(); });

            var host = TestHelpers.CreatePluginHost(new[] { typeof(EndpointMiddlewareFacts).GetTypeInfo().Assembly, typeof(EndpointDescriptor).GetTypeInfo().Assembly });
            var middleware = new EndpointMiddleware(_next, host, new LoggerFactory());

            var context = new DefaultHttpContext();
            context.Request.Path = PathString.FromUriComponent("/gotodefinition");

            var memoryStream = new MemoryStream();

            context.Request.Body = new MemoryStream(
                Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new GotoDefinitionRequest
                    {
                        FileName = "bar.cs",
                        Line = 2,
                        Column = 14,
                        Timeout = 60000
                    })
                )
            );

            await middleware.Invoke(context);

            Assert.True(true);
        }

        [Fact]
        public async Task Passes_through_to_all_services_with_delegate()
        {
            RequestDelegate _next = (ctx) => Task.Run(() => { throw new NotImplementedException(); });

            var host = TestHelpers.CreatePluginHost(new[] { typeof(EndpointMiddlewareFacts).GetTypeInfo().Assembly, typeof(EndpointDescriptor).GetTypeInfo().Assembly });
            var middleware = new EndpointMiddleware(_next, host, new LoggerFactory());

            var context = new DefaultHttpContext();
            context.Request.Path = PathString.FromUriComponent("/findsymbols");

            var memoryStream = new MemoryStream();

            context.Request.Body = new MemoryStream(
                Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new FindSymbolsRequest
                    {

                    })
                )
            );

            await middleware.Invoke(context);

            Assert.True(true);
        }

        [Fact]
        public async Task Passes_through_to_specific_service_with_delegate()
        {
            RequestDelegate _next = (ctx) => Task.Run(() => { throw new NotImplementedException(); });

            var host = TestHelpers.CreatePluginHost(new[] { typeof(EndpointMiddlewareFacts).GetTypeInfo().Assembly, typeof(EndpointDescriptor).GetTypeInfo().Assembly });
            var middleware = new EndpointMiddleware(_next, host, new LoggerFactory());

            var context = new DefaultHttpContext();
            context.Request.Path = PathString.FromUriComponent("/findsymbols");

            var memoryStream = new MemoryStream();

            context.Request.Body = new MemoryStream(
                Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new FindSymbolsRequest
                    {
                        Language = LanguageNames.CSharp
                    })
                )
            );

            await middleware.Invoke(context);

            Assert.True(true);
        }

        public Func<ThrowRequest, Task<ThrowResponse>> ThrowDelegate = (request) =>
        {
            return Task.FromResult<ThrowResponse>(null);
        };

        [OmniSharpEndpoint("/throw", typeof(ThrowRequest), typeof(ThrowResponse))]
        public class ThrowRequest : IRequest { }
        public class ThrowResponse { }

        [Fact]
        public async Task Should_throw_if_type_is_not_mergeable()
        {
            RequestDelegate _next = async (ctx) => await Task.Run(() => { throw new NotImplementedException(); });

            var host = TestHelpers.CreatePluginHost(new[] { typeof(EndpointMiddlewareFacts).GetTypeInfo().Assembly });
            var middleware = new EndpointMiddleware(_next, host, new LoggerFactory());

            var context = new DefaultHttpContext();
            context.Request.Path = PathString.FromUriComponent("/throw");

            var memoryStream = new MemoryStream();

            context.Request.Body = new MemoryStream(
                Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(new ThrowRequest())
                )
            );

            await Assert.ThrowsAsync<NotSupportedException>(async () => await middleware.Invoke(context));
        }
    }
}
