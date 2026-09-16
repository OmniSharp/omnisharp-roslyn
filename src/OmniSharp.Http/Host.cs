using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
#if NETCOREAPP
using Microsoft.Extensions.Hosting;
#endif
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
        private readonly PluginAssemblies _commandLinePlugins;
        private readonly int _serverPort;
        private readonly string _serverInterface;

        public Host(
            IOmniSharpEnvironment environment,
            ISharedTextWriter sharedTextWriter,
            PluginAssemblies commandLinePlugins,
            int serverPort,
            string serverInterface)
        {
            _environment = environment;
            _sharedTextWriter = sharedTextWriter;
            _commandLinePlugins = commandLinePlugins;
            _serverPort = serverPort;
            _serverInterface = serverInterface;
        }

        public void Start()
        {
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddCommandLine(new[] { "--server.urls", $"http://{_serverInterface}:{_serverPort}" });

#if NETCOREAPP
            using (var app = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureWebHost(builder => builder
                    .UseKestrel(config =>
                    {
                        config.AllowSynchronousIO = true;
                    })
                    .ConfigureServices(serviceCollection =>
                    {
                        serviceCollection.AddSingleton(_environment);
                        serviceCollection.AddSingleton(_sharedTextWriter);
                        serviceCollection.AddSingleton(NullEventEmitter.Instance);
                        serviceCollection.AddSingleton(_commandLinePlugins);
                        serviceCollection.AddSingleton(new HttpEnvironment { Port = _serverPort });
                    })
                    .UseUrls($"http://{_serverInterface}:{_serverPort}")
                    .UseConfiguration(config.Build())
                    .UseEnvironment("OmniSharp")
                    .UseStartup(typeof(Startup)))
                .Build())
#else
            using (var app = new WebHostBuilder()
                .UseKestrel()
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddSingleton(_environment);
                    serviceCollection.AddSingleton(_sharedTextWriter);
                    serviceCollection.AddSingleton(NullEventEmitter.Instance);
                    serviceCollection.AddSingleton(_commandLinePlugins);
                    serviceCollection.AddSingleton(new HttpEnvironment { Port = _serverPort });
                })
                .UseUrls($"http://{_serverInterface}:{_serverPort}")
                .UseConfiguration(config.Build())
                .UseEnvironment("OmniSharp")
                .UseStartup(typeof(Startup))
                .Build())
#endif
            {
                app.Start();

#if NETCOREAPP
                var appLifeTime = app.Services.GetRequiredService<IHostApplicationLifetime>();
#else
                var appLifeTime = app.Services.GetRequiredService<IApplicationLifetime>();
#endif

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
