using System;
using System.Diagnostics;
using Microsoft.AspNet.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Plugins;
using OmniSharp.Services;
using OmniSharp.Stdio;
using OmniSharp.Stdio.Services;

namespace OmniSharp.Host
{
    public static class HostRunner
    {
        public static void Run(string[] args)
        {
            Run<Startup>(args);
        }

        public static void Run<TStartup>(string[] args) where TStartup : class
        {
            var arguments = ProgramArguments.Parse(args);

            var config = new ConfigurationBuilder().AddCommandLine(
                new[] { "--server.urls", $"http://localhost:{arguments.ServerPort}" });

            var writer = new SharedConsoleWriter();

            var host = new WebHostBuilder()
                .UseConfiguration(config.Build())
                .UseEnvironment("OmniSharp")
                .UseStartup<TStartup>()
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddSingleton<IOmnisharpEnvironment>(arguments.Environment);
                    serviceCollection.AddSingleton<ISharedTextWriter>(writer);
                    serviceCollection.AddSingleton(new PluginAssemblies(arguments.Plugins));
                });

            if (arguments.TransportType == TransportType.Stdio)
            {
                host.UseServer(new StdioServerFactory(Console.In, writer));
            }
            else
            {
                host.UseServer("Microsoft.AspNet.Server.Kestrel");
            }

            using (var app = host.Build())
            {
                app.Start();

                var appLifeTime = app.Services.GetRequiredService<IApplicationLifetime>();

                Console.CancelKeyPress += (sender, e) =>
                {
                    appLifeTime.StopApplication();
                    e.Cancel = true;
                };

                if (arguments.HostProcesssID != -1)
                {
                    try
                    {
                        var hostProcess = Process.GetProcessById(arguments.HostProcesssID);
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
