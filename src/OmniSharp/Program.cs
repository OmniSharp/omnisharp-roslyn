using System;
using System.Linq;
using System.IO;
using OmniSharp.Services;
using Microsoft.Framework.Logging;
using Microsoft.AspNet.Hosting;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.DependencyInjection.Fallback;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;

namespace OmniSharp
{
    public class Program
    {
        private readonly IServiceProvider _serviceProvider;

        public Program(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: Omnisharp [-s /path/to/sln] [-p port]");
                return;
            }

            var applicationRoot = Directory.GetCurrentDirectory();
            var serverPort = 2000;
            var traceType = TraceType.Information;

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
                    traceType = TraceType.Verbose;
                }
            }

            var environment = new OmnisharpEnvironment(applicationRoot, serverPort, traceType);
            var hostingEnv = new HostingEnvironment { EnvironmentName = "Development" };

            var config = new Configuration();
            config.Set("server.urls", "http://localhost:" + serverPort);

            var serviceCollection = new ServiceCollection();
            serviceCollection.Add(HostingServices.GetDefaultServices(config));
            serviceCollection.AddInstance<IOmnisharpEnvironment>(environment);
            serviceCollection.AddInstance<IHostingEnvironment>(hostingEnv);

            var services = serviceCollection
                .BuildServiceProvider(_serviceProvider);

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
            var appShutdownService = _serviceProvider.GetRequiredService<IApplicationShutdown>();
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
            Console.CancelKeyPress += (sender, e)=>
            {
                appShutdownService.RequestShutdown();
            };
#endif

            shutdownHandle.Wait();
        }
    }
}