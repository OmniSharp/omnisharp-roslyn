using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Services;

namespace OmniSharp.Plugins
{
    public class Plugin : IProjectSystem, IDisposable
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly Process _process = null;
        private readonly ILogger<Plugin> _logger;
        private readonly ConcurrentDictionary<int, Action<string>> _requests = new ConcurrentDictionary<int, Action<string>>();
        public PluginConfig Config { get; set; }

        public Plugin(ILogger<Plugin> logger, PluginConfig config)
        {
            _logger = logger;
            _cancellation = new CancellationTokenSource();
            Config = config;

            Key = Config.Name;
            Language = Config.Language;
            Extensions = Config.Extensions;
        }

        public string Key { get; }
        public string Language { get; }
        public IEnumerable<string> Extensions { get; }
        public bool EnabledByDefault { get; } = true;
        public bool Initialized { get; private set; }

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
                            _logger.LogError(response.Message);
                            return;
                        }

                        if (!_requests.TryGetValue(response.Request_seq, out Action<string> requestHandler))
                        {
                            throw new ArgumentException("invalid seq-value");
                        }

                        requestHandler(response.BodyJson);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e.ToString());
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
            if (Initialized) return;
            Initialized = true;
            Task.Run(() => Run());
        }

        public Task<object> GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            // TODO: Call out to process
            return Task.FromResult<object>(null);
        }

        public Task<object> GetProjectModelAsync(string filePath)
        {
            // TODO: Call out to process
            return Task.FromResult<object>(null);
        }
    }
}
