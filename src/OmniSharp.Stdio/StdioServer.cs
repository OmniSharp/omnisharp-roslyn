using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Stdio.Features;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Stdio
{
    class StdioServer : IServer
    {
        private readonly TextReader _input;
        private readonly ISharedTextWriter _writer;
        private readonly CancellationTokenSource _cancellation;
        private readonly IHttpContextFactory _httpContextFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly object _lock = new object();

        public StdioServer(TextReader input, ISharedTextWriter writer)
        {
            _input = input;
            _writer = writer;
            _cancellation = new CancellationTokenSource();

            _httpContextAccessor = new HttpContextAccessor();
            _httpContextFactory = new HttpContextFactory(_httpContextAccessor);

            var features = new FeatureCollection();
            var requestFeature = new RequestFeature();
            var responseFeature = new ResponseFeature();

            features.Set<IHttpRequestFeature>(requestFeature);
            features.Set<IHttpResponseFeature>(responseFeature);
            Features = features;
        }

        public IFeatureCollection Features { get; }

        public void Start<TContext>(IHttpApplication<TContext> application)
        {
            Task.Factory.StartNew(async () =>
            {
                _writer.WriteLine(new EventPacket()
                {
                    Event = "started"
                });

                while (!_cancellation.IsCancellationRequested)
                {
                    var line = await _input.ReadLineAsync();
                    if (line == null)
                    {
                        break;
                    }

                    var ignored = Task.Factory.StartNew(async () =>
                    {
                        try
                        {
                            await HandleRequest(line, application);
                        }
                        catch (Exception e)
                        {
                            _writer.WriteLine(new EventPacket()
                            {
                                Event = "error",
                                Body = JsonConvert.ToString(e.ToString(), '"', StringEscapeHandling.Default)
                            });
                        }
                    });
                }
            });
        }

        public void Dispose()
        {
            _cancellation.Cancel();
        }

        private async Task HandleRequest<TContext>(string json, IHttpApplication<TContext> application)
        {
            var request = RequestPacket.Parse(json);
            var response = request.Reply();

            using (var inputStream = request.ArgumentsStream)
            using (var outputStream = new MemoryStream())
            {
                try
                {
                    var features = new FeatureCollection();
                    var requestFeature = new RequestFeature();
                    var responseFeature = new ResponseFeature();

                    requestFeature.Path = request.Command;
                    requestFeature.Body = inputStream;
                    requestFeature.Headers["Content-Type"] = new[] { "application/json" };
                    responseFeature.Body = outputStream;

                    features.Set<IHttpRequestFeature>(requestFeature);
                    features.Set<IHttpResponseFeature>(responseFeature);

                    var context = application.CreateContext(features);

                    // hand off request to next layer
                    await application.ProcessRequestAsync(context);

                    if (responseFeature.StatusCode != 200)
                    {
                        response.Success = false;
                    }

                    // HttpResponse stream becomes body as is
                    var buffer = outputStream.ToArray();
                    if (buffer.Length > 0)
                    {
                        response.Body = new JRaw(new String(Encoding.UTF8.GetChars(buffer, 0, buffer.Length)));
                    }
                }
                catch (Exception e)
                {
                    // updating the response object here so that the ResponseStream
                    // prints the latest state when being closed
                    response.Success = false;
                    response.Message = JsonConvert.ToString(e.ToString(), '"', StringEscapeHandling.Default);
                }
                finally
                {
                    // actually write it
                    _writer.WriteLine(response);
                }
            }
        }
    }
}
