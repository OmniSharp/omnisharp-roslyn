using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.FeatureModel;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Authentication;
using Microsoft.AspNet.Http.Core;
using Microsoft.CodeAnalysis;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json;
using OmniSharp.Filters;
using OmniSharp.Mef;
using OmniSharp.Middleware;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services;
using Xunit;

namespace OmniSharp.Tests
{
    public class EndpointMiddlewareFacts
    {
        [OmniSharpEndpoint(typeof(Endpoints.GotoDefinition), LanguageNames.CSharp)]
        public class GotoDefinitionService : Endpoints.GotoDefinition
        {
            [Import]
            public OmnisharpWorkspace Workspace { get; set; }

            public Task<GotoDefinitionResponse> GotoDefinition(GotoDefinitionRequest request)
            {
                //throw new NotImplementedException();
                return Task.FromResult<GotoDefinitionResponse>(null);
            }
        }

        public class CSharpLanguage
        {
            private static readonly string[] ValidCSharpExtensions = { "cs", "csx", "cake" };
            [OmniSharpLanguage(LanguageNames.CSharp)]
            public Func<string, Task<bool>> IsApplicableTo { get; } = filePath => Task.FromResult(ValidCSharpExtensions.Any(extension => filePath.EndsWith(extension)));
        }

        class LoggerFactory : ILoggerFactory
        {
            public LogLevel MinimumLevel { get; set; }
            public void AddProvider(ILoggerProvider provider) { }
            public ILogger CreateLogger(string categoryName) { return new Logger(); }
        }

        class Disposable : IDisposable { public void Dispose() { } }

        class Logger : ILogger
        {
            public IDisposable BeginScope(object state) { return new Disposable(); }
            public bool IsEnabled(LogLevel logLevel) { return true; }
            public void Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter) { }
        }

        [Fact]
        public Task Passes_through_for_invalid_path()
        {
            RequestDelegate _next = (ctx) => Task.Run(() => { throw new NotImplementedException(); });

            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>());
            workspace.ConfigurePluginHost(new[] { typeof(EndpointMiddlewareFacts).GetTypeInfo().Assembly });
            var middleware = new EndpointMiddleware(_next, workspace, new LoggerFactory());

            var context = new DefaultHttpContext();
            context.Request.Path = PathString.FromUriComponent("/notvalid");

            return Assert.ThrowsAsync<NotImplementedException>(() => middleware.Invoke(context));
        }

        [Fact]
        public async Task Does_not_throw_for_valid_path()
        {
            RequestDelegate _next = (ctx) => Task.Run(() => { throw new NotImplementedException(); });

            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string>());
            workspace.ConfigurePluginHost(new[] { typeof(EndpointMiddlewareFacts).GetTypeInfo().Assembly });
            var middleware = new EndpointMiddleware(_next, workspace, new LoggerFactory());

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

            var source1 = @"using System;

    class Foo {
    }";
            var source2 = @"class Bar {
        private Foo foo;
    }";

            var workspace = TestHelpers.CreateSimpleWorkspace(new Dictionary<string, string> {
                    { "foo.cs", source1 }, { "bar.cs", source2}
                });
            workspace.ConfigurePluginHost(new[] { typeof(EndpointMiddlewareFacts).GetTypeInfo().Assembly });
            var middleware = new EndpointMiddleware(_next, workspace, new LoggerFactory());

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
        }
    }
}
