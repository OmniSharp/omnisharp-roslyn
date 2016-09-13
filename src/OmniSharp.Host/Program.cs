using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Host.Loader;
using OmniSharp.Plugins;
using OmniSharp.Services;
using OmniSharp.Stdio;
using OmniSharp.Stdio.Services;

namespace OmniSharp
{
    public class Program
    {
        public static OmnisharpEnvironment Environment { get; set; }

        public static void Main(string[] args)
        {
            Console.WriteLine($"OmniSharp: {string.Join(" ", args)}");

            var applicationRoot = Directory.GetCurrentDirectory();
            var serverPort = 2000;
            var logLevel = LogLevel.Information;
            var hostPID = -1;
            var transportType = TransportType.Http;
            var otherArgs = new List<string>();
            var plugins = new List<string>();
            var serverInterface = "localhost";
            Encoding encoding = null;

            var enumerator = args.GetEnumerator();

            while (enumerator.MoveNext())
            {
                var arg = (string)enumerator.Current;
                if (arg == "-s")
                {
                    enumerator.MoveNext();
                    applicationRoot = Path.GetFullPath((string)enumerator.Current);
                }
                else if (arg == "-p")
                {
                    enumerator.MoveNext();
                    serverPort = int.Parse((string)enumerator.Current);
                }
                else if (arg == "-v")
                {
                    logLevel = LogLevel.Debug;
                }
                else if (arg == "--hostPID")
                {
                    enumerator.MoveNext();
                    hostPID = int.Parse((string)enumerator.Current);
                }
                else if (arg == "--stdio")
                {
                    transportType = TransportType.Stdio;
                }
                else if (arg == "--plugin")
                {
                    enumerator.MoveNext();
                    plugins.Add((string)enumerator.Current);
                }
                else if (arg == "--zero-based-indices")
                {
                    Configuration.ZeroBasedIndices = true;
                }
                else if (arg == "--interface")
                {
                    enumerator.MoveNext();
                    serverInterface = (string)enumerator.Current;
                }
                else if (arg == "--encoding")
                {
                    enumerator.MoveNext();
                    encoding = Encoding.GetEncoding((string)enumerator.Current);
                }
                else
                {
                    otherArgs.Add((string)enumerator.Current);
                }
            }

            Environment = new OmnisharpEnvironment(applicationRoot, serverPort, hostPID, logLevel, transportType, otherArgs.ToArray(), plugins.ToArray());

            var config = new ConfigurationBuilder()
                .AddCommandLine(new[] { "--server.urls", $"http://{serverInterface}:{serverPort}" });

            // If the --encoding switch was specified, we need to set the InputEncoding and OutputEncoding before
            // constructing the SharedConsoleWriter. Otherwise, it might be created with the wrong encoding since
            // it wraps around Console.Out, which gets recreated when OutputEncoding is set.
            if (transportType == TransportType.Stdio && encoding != null)
            {
                Console.InputEncoding = encoding;
                Console.OutputEncoding = encoding;
            }

            var writer = new SharedConsoleWriter();

            var builder = new WebHostBuilder()
                .UseConfiguration(config.Build())
                .UseEnvironment("OmniSharp")
                .UseStartup(typeof(Startup))
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddSingleton<IOmnisharpEnvironment>(Environment);
                    serviceCollection.AddSingleton<ISharedTextWriter>(writer);
                    serviceCollection.AddSingleton<PluginAssemblies>(new PluginAssemblies(plugins));
                    serviceCollection.AddSingleton<IOmnisharpAssemblyLoader>(new OmnisharpAssemblyLoader());
                });

            if (transportType == TransportType.Stdio)
            {
                builder.UseServer(new StdioServer(Console.In, writer));
            }
            else
            {
                builder.UseKestrel();
            }

            using (var app = builder.Build())
            {
                app.Start();

                var appLifeTime = app.Services.GetRequiredService<IApplicationLifetime>();

                Console.CancelKeyPress += (sender, e) =>
                {
                    appLifeTime.StopApplication();
                    e.Cancel = true;
                };

                if (hostPID != -1)
                {
                    try
                    {
                        var hostProcess = Process.GetProcessById(hostPID);
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
