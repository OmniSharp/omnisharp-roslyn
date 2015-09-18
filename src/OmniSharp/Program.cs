using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Hosting.Internal;
using Microsoft.AspNet.Hosting.Startup;
using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using OmniSharp.Plugins;
using OmniSharp.Services;
using OmniSharp.Stdio.Services;

namespace OmniSharp
{
    public class Program
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IStartupLoader _startupLoader;

        public static OmnisharpEnvironment Environment { get; set; }

        public Program(IServiceProvider serviceProvider, IStartupLoader startupLoader)
        {
            _serviceProvider = serviceProvider;
            _startupLoader = startupLoader;
        }

        public void Main(string[] args)
        {
            var applicationRoot = Directory.GetCurrentDirectory();
            var serverPort = 2000;
            var logLevel = LogLevel.Information;
            var hostPID = -1;
            var transportType = TransportType.Http;
            var otherArgs = new List<string>();
            var plugins = new List<string>();

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
                    logLevel = LogLevel.Verbose;
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
                else if (arg == "--plugin") {
                    enumerator.MoveNext();
                    plugins.Add((string)enumerator.Current);
                }
                else
                {
                    otherArgs.Add((string)enumerator.Current);
                }
            }

            Environment = new OmnisharpEnvironment(applicationRoot, serverPort, hostPID, logLevel, transportType, otherArgs.ToArray());

            var config = new ConfigurationBuilder()
             .AddCommandLine(new[] { "--server.urls", "http://localhost:" + serverPort });

            var serviceCollection = new ServiceCollection();

            var writer = new SharedConsoleWriter();
            serviceCollection.AddInstance<IOmnisharpEnvironment>(Environment);
            serviceCollection.AddInstance<ISharedTextWriter>(writer);
            serviceCollection.AddInstance<PluginAssemblies>(new PluginAssemblies(plugins));

            var engine = new HostingEngine(serviceCollection, _startupLoader, config.Build());

            if (transportType == TransportType.Stdio)
            {
                // Uppercase variable is intentional here
                var ServerFactory = new Stdio.StdioServerFactory(Console.In, writer);
                var propertyInfo = engine.GetType().GetTypeInfo().DeclaredProperties.First(prop => prop.Name == nameof(ServerFactory));
                propertyInfo.SetValue(engine, ServerFactory);
            } else {
                // Uppercase variable is intentional here
                var ServerFactoryLocation = "Kestrel";
                var propertyInfo = engine.GetType().GetTypeInfo().DeclaredProperties.First(prop => prop.Name == nameof(ServerFactoryLocation));
                propertyInfo.SetValue(engine, ServerFactoryLocation);
            }

            var serverShutdown = engine.Start();

            var appShutdownService = _serviceProvider.GetRequiredService<IApplicationShutdown>();
            var shutdownHandle = new ManualResetEvent(false);

            appShutdownService.ShutdownRequested.Register(() =>
            {
                serverShutdown.Dispose();
                shutdownHandle.Set();
            });

#if DNXCORE50
            var ignored = Task.Run(() =>
            {
                Console.WriteLine("Started");
                Console.ReadLine();
                appShutdownService.RequestShutdown();
            });
#else
            Console.CancelKeyPress += (sender, e) =>
            {
                appShutdownService.RequestShutdown();
            };
#endif

            if (hostPID != -1)
            {
                try
                {
                    var hostProcess = Process.GetProcessById(hostPID);
                    hostProcess.EnableRaisingEvents = true;
                    hostProcess.OnExit(() => appShutdownService.RequestShutdown());
                }
                catch
                {
                    // If the process dies before we get here then request shutdown
                    // immediately
                    appShutdownService.RequestShutdown();
                }
            }

            shutdownHandle.WaitOne();
        }
    }
}
