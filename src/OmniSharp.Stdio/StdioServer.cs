using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;
using Microsoft.AspNet.Http.Internal;
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
        private readonly RequestFeature _requestFeature;
        private readonly ResponseFeature _responseFeature;
        private readonly IHttpContextFactory _httpContextFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public StdioServer(TextReader input, ISharedTextWriter writer)
        {
            _input = input;
            _writer = writer;
            _cancellation = new CancellationTokenSource();

            _httpContextAccessor = new HttpContextAccessor();
            _httpContextFactory = new HttpContextFactory(_httpContextAccessor);

            var features = new FeatureCollection();
            _requestFeature = new RequestFeature();
            _responseFeature = new ResponseFeature();

            features.Set<IHttpRequestFeature>(_requestFeature);
            features.Set<IHttpResponseFeature>(_responseFeature);
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
                    _requestFeature.Reset();
                    _requestFeature.Path = request.Command;
                    _requestFeature.Body = inputStream;
                    _requestFeature.Headers["Content-Type"] = new[] { "application/json" };

                    _responseFeature.Reset();
                    _responseFeature.Body = outputStream;

                    var context = application.CreateContext(Features);

                    // hand off request to next layer
                    await application.ProcessRequestAsync(context);

                    if (_responseFeature.StatusCode != 200)
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
