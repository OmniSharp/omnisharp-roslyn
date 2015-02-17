using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNet.Hosting;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;
using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime;
using OmniSharp.Models;
using OmniSharp.Services;
using OmniSharp.Stdio.Protocol;
using OmniSharp.Stdio.Transport;

namespace OmniSharp.Stdio
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
            var applicationRoot = Directory.GetCurrentDirectory();
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

            OmniSharp.Program.Environment = new OmnisharpEnvironment(applicationRoot, -1, hostPID, logLevel);

            var services = new ServiceCollection();
            var startup = new Startup();
            startup.ConfigureServices(services, new ApplicationLifetime());

            var provider = services.BuildServiceProvider();
            startup.Configure(provider, provider.GetRequiredService<ILoggerFactory>(), OmniSharp.Program.Environment);

            var appShutdownService = provider.GetRequiredService<IApplicationShutdown>();
            var handler = new RequestHandler(provider, arg =>
            {
                if (arg is Request)
                {
                    startup.Workspace.EnsureBufferUpdated((Request)arg);
                }
                return arg;
            });

            Console.WriteLine(new EventPacket()
            {
                Event = "started"
            });

            while (!appShutdownService.ShutdownRequested.IsCancellationRequested)
            {
                var line = Console.ReadLine();
                Task.Factory.StartNew(async () =>
                {
                    var response = await handler.HandleRequest(line);
                    Console.WriteLine(response);
                });
            }
        }
    }
}
