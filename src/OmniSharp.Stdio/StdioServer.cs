using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.FeatureModel;
using Microsoft.AspNet.Http.Interfaces;
using OmniSharp.Stdio.Features;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Stdio
{
    class StdioServer : IDisposable
    {
        private readonly TextReader _input;
        private readonly ISharedTextWriter _output;
        private readonly Func<object, Task> _next;
        private readonly CancellationTokenSource _cancellation;

        public StdioServer(TextReader input, ISharedTextWriter output, Func<object, Task> next)
        {
            _input = input;
            _output = output;
            _next = next;
            _cancellation = new CancellationTokenSource();

            var ignored = Run();
        }

        private async Task Run()
        {
            _output.Use(writer =>
            {
                writer.WriteLine(new EventPacket()
                {
                    Event = "started"
                });
            });

            while (!_cancellation.IsCancellationRequested)
            {
                var line = await _input.ReadLineAsync();
                var ignored = Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        await HandleRequest(line);
                    }
                    catch (Exception e)
                    {
                        _output.Use(writer =>
                        {
                            writer.Write(new EventPacket()
                            {
                                Event = "error",
                                Body = e.ToString()
                            });
                        });
                    }
                });
            }
        }

        private async Task HandleRequest(string json)
        {
            var request = RequestPacket.Parse(json);
            var response = request.Reply();

            using (var inputStream = request.ArgumentsStream)
            using (var outputStream = new StdioResponseStream(_output, response))
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
                }
                catch (Exception e)
                {
                    // updating the response object here so that the ResponseStream
                    // prints the latest state when being closed
                    response.Success = false;
                    response.Message = e.ToString();
                }
            }
        }

        public void Dispose()
        {
            _cancellation.Cancel();
        }
    }
}
