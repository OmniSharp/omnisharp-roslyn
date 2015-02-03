using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Services;
using Microsoft.Framework.Logging;
using Microsoft.AspNet.Hosting;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.DependencyInjection.Fallback;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.DependencyInjection;

namespace OmniSharp
{
    public class Program
    {
        private readonly IServiceProvider _serviceProvider;

        public static OmnisharpEnvironment Environment { get; set; }

        public Program(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Main(string[] args)
        {
            var applicationRoot = Directory.GetCurrentDirectory();
            var serverPort = 2000;
            var logLevel = LogLevel.Information;
            var hostPID = -1;
            
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
            }

            Environment = new OmnisharpEnvironment(applicationRoot, serverPort, hostPID, logLevel);

            var config = new Configuration()
             .AddCommandLine(new[] { "--server.urls", "http://localhost:" + serverPort });

            var serviceCollection = HostingServices.Create(_serviceProvider, config);

            var services = serviceCollection.BuildServiceProvider();
            var hostingEnv = services.GetRequiredService<IHostingEnvironment>();
            var appEnv = services.GetRequiredService<IApplicationEnvironment>();

            var context = new HostingContext()
            {
                Services = services,
                Configuration = config,
                ServerName = "Kestrel",
                ApplicationName = appEnv.ApplicationName,
                EnvironmentName = hostingEnv.EnvironmentName,
            };

            var engine = services.GetRequiredService<IHostingEngine>();
            var appShutdownService = services.GetRequiredService<IApplicationShutdown>();
            var shutdownHandle = new ManualResetEventSlim(false);

            var serverShutdown = engine.Start(context);

            appShutdownService.ShutdownRequested.Register(() =>
            {
                serverShutdown.Dispose();
                shutdownHandle.Set();
            });

#if ASPNETCORE50
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
                var hostProcess = Process.GetProcessById(hostPID);
                hostProcess.EnableRaisingEvents = true;
                hostProcess.OnExit(() => appShutdownService.RequestShutdown());
            }
            
            shutdownHandle.Wait();
        }
    }
}