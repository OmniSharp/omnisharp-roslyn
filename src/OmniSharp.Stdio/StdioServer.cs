using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.FeatureModel;
using Microsoft.AspNet.HttpFeature;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Stdio.Features;
using OmniSharp.Stdio.Protocol;

namespace OmniSharp.Stdio
{
    class StdioServer : IDisposable
    {
        private readonly TextReader _input;
        private readonly TextWriter _output;
        private readonly CancellationTokenSource _cancellation;
        private readonly Func<object, Task> _next;

        public StdioServer(TextReader input, TextWriter output, Func<object, Task> next)
        {
            _input = input;
            _output = output;
            _cancellation = new CancellationTokenSource();
            _next = next;

            Start();
        }

        private void Start()
        {
            _output.WriteLine(new EventPacket() {
                Event = "started"
            });

            while (!_cancellation.IsCancellationRequested)
            {
                var line = _input.ReadLine();
                var ignored = Task.Factory.StartNew(async () =>
                {
                    var packet = await HandleRequest(line);
                    _output.WriteLine(packet);
                });
            }
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
                    var httpRequest = new RequestFeature()
                    {
                        Body = inputStream,
                        Path = request.Command[0] == '/'
                            ? request.Command
                            : "/" + request.Command
                    };
                    var httpResponse = new ResponseFeature()
                    {
                        Body = outputStream
                    };
                    var collection = new FeatureCollection();
                    collection[typeof(IHttpRequestFeature)] = httpRequest;
                    collection[typeof(IHttpResponseFeature)] = httpResponse;

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
