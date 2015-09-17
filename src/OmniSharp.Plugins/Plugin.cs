using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Models.v1;
using OmniSharp.Services;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Plugins
{
    public class Plugin : IProjectSystem, IDisposable
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly Process _process = null;
        private readonly ISharedTextWriter _writer;
        private readonly ConcurrentDictionary<int, Action<string>> _requests = new ConcurrentDictionary<int, Action<string>>();
        public PluginConfig Config { get; set; }

        public Plugin(ISharedTextWriter writer, PluginConfig config)
        {
            _writer = writer;
            _cancellation = new CancellationTokenSource();
            Config = config;

            Key = Config.Name;
            Language = Config.Language;
            Extensions = Config.Extensions;
        }

        public string Key { get; }
        public string Language { get; }
        public IEnumerable<string> Extensions { get; }

        public Task<TResponse> Handle<TRequest, TResponse>(string endpoint, TRequest request)
        {
            var oopRequest = new PluginRequest()
            {
                Command = endpoint,
                Arguments = request
            };

            _process.StandardInput.WriteLine(JsonConvert.SerializeObject(oopRequest));
            // Complete Task
            var tcs = new TaskCompletionSource<TResponse>();

            _requests.TryAdd(oopRequest.Seq, (result) =>
            {
                var response = JsonConvert.DeserializeObject<TResponse>(result);
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

                var ignored = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        var response = PluginResponse.Parse(line);

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
                        if (!_requests.TryGetValue(response.Request_seq, out requestHandler))
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
                });
            }
        }

        public void Dispose()
        {
            _cancellation.Cancel();
        }

        public void Initalize(IConfiguration configuration)
        {
            Task.Run(() => Run());
        }

        public Task<object> GetInformationModel(WorkspaceInformationRequest request)
        {
            // TODO: Call out to process
            return null;
        }

        public Task<object> GetProjectModel(string path)
        {
            // TODO: Call out to process
            return null;
        }
    }
}
