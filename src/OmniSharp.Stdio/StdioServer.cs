using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.FeatureModel;
using Microsoft.AspNet.HttpFeature;
using Newtonsoft.Json.Linq;
using OmniSharp.Stdio.Features;
using OmniSharp.Stdio.Protocol;

namespace OmniSharp.Stdio
{
    class StdioServer : IDisposable
    {
        private readonly TextReader _input;
        private readonly TextWriter _output;
        private readonly Func<object, Task> _next;
        private readonly CancellationTokenSource _cancellation;

        public StdioServer(TextReader input, TextWriter output, Func<object, Task> next)
        {
            _input = input;
            _output = output;
            _next = next;
            _cancellation = new CancellationTokenSource();

            Run();
        }

        private void Run()
        {
            Task.Factory.StartNew(async () =>
            {
                _output.WriteLine(new EventPacket() {
                    Event = "started"
                });
    
                while (!_cancellation.IsCancellationRequested)
                {
                    var line = await _input.ReadLineAsync();
                    var ignored = Task.Factory.StartNew(async () =>
                    {
                        var packet = await HandleRequest(line);
                        _output.WriteLine(packet);
                    });
                }
            });
        }

        private async Task<Packet> HandleRequest(string json)
        {
            RequestPacket request;
            try
            {
                request = new RequestPacket(json);
            }
            catch (Exception e)
            {
                return new EventPacket()
                {
                    Event = "error",
                    Body = e.ToString()
                };
            }

            var response = request.Reply();
            using (var inputStream = request.ArgumentsAsStream())
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

                    if(httpResponse.StatusCode != 200)
                    {
                        response.Success = false;
                    }

                    var data = outputStream.ToArray();
                    response.Body = new JRaw(System.Text.Encoding.UTF8.GetString(data));
                }
                catch (Exception e)
                {
                    response.Success = false;
                    response.Message = e.ToString();
                }
            }
            return response;
        }

        public void Dispose()
        {
            _cancellation.Cancel();
        }
    }
}
