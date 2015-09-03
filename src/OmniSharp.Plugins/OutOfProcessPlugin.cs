using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OmniSharp.Plugins
{
    public class OutOfProcessPlugin
    {
        public OutOfProcessPlugin(string[] supportedEndpoints)
        {
            SupportedEndpoints = supportedEndpoints;

            Task.Run(() => Run());
        }

        public string[] SupportedEndpoints { get; }
        public string Language { get; set; }

        private ConcurrentDictionary<int, RequestItem> _requests = new ConcurrentDictionary<int, RequestItem>();
        private Process _process;

        public Task<object> Handle(string endpoint, object Request, Type responseType)
        {
            var request = new RequestItem()
            {
                Command = endpoint,
                Body = Request
            };

            //_requests.TryAdd(request.Seq, request);
            _process.StandardInput.WriteLine(JsonConvert.SerializeObject(request));
            new Task

        }

        private async Task Run()
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
        }
    }

    class RequestItem
    {
        public int Seq { get; set; }
        public string Command { get; set; }
        public object Body { get; set; }
    }
}
