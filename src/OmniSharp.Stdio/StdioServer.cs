using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;
using Newtonsoft.Json.Linq;
using OmniSharp.Stdio.Features;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Stdio
{
    class StdioServer : IDisposable
    {
        private readonly TextReader _input;
        private readonly ISharedTextWriter _writer;
        private readonly Func<IFeatureCollection, Task> _next;
        private readonly CancellationTokenSource _cancellation;

        public StdioServer(TextReader input, ISharedTextWriter writer, Func<IFeatureCollection, Task> next)
        {
            _input = input;
            _writer = writer;
            _next = next;
            _cancellation = new CancellationTokenSource();

            Run();
        }

        private void Run()
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
                            await HandleRequest(line);
                        }
                        catch (Exception e)
                        {
                            _writer.WriteLine(new EventPacket()
                            {
                                Event = "error",
                                Body = e.ToString()
                            });
                        }
                    });
                }
            });
        }

        private async Task HandleRequest(string json)
        {
            var request = RequestPacket.Parse(json);
            var response = request.Reply();

            using (var inputStream = request.ArgumentsStream)
            using (var outputStream = new MemoryStream())
            {
                try
                {
                    var httpRequest = new RequestFeature();
                    httpRequest.Path = request.Command;
                    httpRequest.Body = inputStream;
                    httpRequest.Headers["Content-Type"] = new[] { "application/json" };

                    var httpResponse = new ResponseFeature();
                    httpResponse.Body = outputStream;

                    var collection = new FeatureCollection();
                    collection[typeof(IHttpRequestFeature)] = httpRequest;
                    collection[typeof(IHttpResponseFeature)] = httpResponse;

                    // hand off request to next layer
                    await _next(collection);

                    if (httpResponse.StatusCode != 200)
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
                    response.Message = e.ToString();
                }
                finally
                {
                    // actually write it
                    _writer.WriteLine(response);
                }
            }
        }

        public void Dispose()
        {
            _cancellation.Cancel();
        }
    }
}
