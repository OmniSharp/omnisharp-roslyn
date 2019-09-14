using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Eventing;
using OmniSharp.Plugins;
using OmniSharp.Services;
using OmniSharp.Utilities;

namespace OmniSharp.Http
{
    internal class Host
    {
        private readonly IOmniSharpEnvironment _environment;
        private readonly ISharedTextWriter _sharedTextWriter;
        private readonly PluginAssemblies _pluginAssemblies;
        private readonly int _serverPort;
        private readonly string _serverInterface;

        public Host(
            IOmniSharpEnvironment environment,
            ISharedTextWriter sharedTextWriter,
            PluginAssemblies pluginAssemblies,
            int serverPort,
            string serverInterface)
        {
            _environment = environment;
            _sharedTextWriter = sharedTextWriter;
            _pluginAssemblies = pluginAssemblies;
            _serverPort = serverPort;
            _serverInterface = serverInterface;
        }

        public void Start()
        {
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddCommandLine(new[] { "--server.urls", $"http://{_serverInterface}:{_serverPort}" });

            var builder = new WebHostBuilder()
                .UseKestrel()
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddSingleton(_environment);
                    serviceCollection.AddSingleton(_sharedTextWriter);
                    serviceCollection.AddSingleton(NullEventEmitter.Instance);
                    serviceCollection.AddSingleton(_pluginAssemblies);
                    serviceCollection.AddSingleton(new HttpEnvironment { Port = _serverPort });
                })
                .UseUrls($"http://{_serverInterface}:{_serverPort}")
                .UseConfiguration(config.Build())
                .UseEnvironment("OmniSharp")
                .UseStartup(typeof(Startup));

            using (var app = builder.Build())
            {
                app.Start();

                var appLifeTime = app.Services.GetRequiredService<IApplicationLifetime>();

                Console.CancelKeyPress += (sender, e) =>
                {
                    appLifeTime.StopApplication();
                    e.Cancel = true;
                };

                if (_environment.HostProcessId != -1)
                {
                    try
                    {
                        var hostProcess = Process.GetProcessById(_environment.HostProcessId);
                        hostProcess.EnableRaisingEvents = true;
                        hostProcess.OnExit(() => appLifeTime.StopApplication());
                    }
                    catch
                    {
                        // If the process dies before we get here then request shutdown
                        // immediately
                        appLifeTime.StopApplication();
                    }
                }

                appLifeTime.ApplicationStopping.WaitHandle.WaitOne();
            }
        }
    }
}
