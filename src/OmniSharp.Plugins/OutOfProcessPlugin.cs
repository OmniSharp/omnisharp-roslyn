using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Plugins
{
    public class OutOfProcessPlugin : IDisposable
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly Process _process = null;
        private readonly ISharedTextWriter _writer;
        private readonly ConcurrentDictionary<int, Action<string>> _requests = new ConcurrentDictionary<int, Action<string>>();
        public OopConfig Config;

        public OutOfProcessPlugin(ISharedTextWriter writer, OopConfig config)
        {
            _writer = writer;
            _cancellation = new CancellationTokenSource();
            Config = config;
            Task.Run(() => Run());
        }

        public Task<object> Handle(string endpoint, object request, Type responseType)
        {
            var oopRequest = new OopRequest()
            {
                Command = endpoint,
                Arguments = request
            };

            _process.StandardInput.WriteLine(JsonConvert.SerializeObject(oopRequest));
            // Complete Task
            var tcs = new TaskCompletionSource<object>();

            _requests.TryAdd(oopRequest.Seq, (result) =>
            {
                var response = JsonConvert.DeserializeObject(result, responseType);
                tcs.SetResult(response);
            });

            return tcs.Task;
        }

        private async Task Run()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                var line = await _process.StandardOutput.ReadLineAsync();
                if (line == null)
                {
                    break;
                }

                var ignored = Task.Factory.StartNew((Action)(() =>
                {
                    try
                    {
                        var response = OopResponse.Parse(line);

                        if (!response.Success)
                        {
                            _writer.WriteLine(new EventPacket()
                            {
                                Event = "error",
                                Body = response.Message,
                            });
                            return;
                        }

                        Action<string> requestHandler = null;
                        if (!_requests.TryGetValue((int)response.Request_seq, out requestHandler))
                        {
                            throw new ArgumentException("invalid seq-value");
                        }

                        requestHandler((string)response.BodyJson);
                    }
                    catch (Exception e)
                    {
                        _writer.WriteLine(new EventPacket()
                        {
                            Event = "error",
                            Body = e.ToString()
                        });
                    }
                }));
            }
        }

        public void Dispose()
        {
            _cancellation.Cancel();
        }
    }
}
