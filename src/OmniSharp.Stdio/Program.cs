using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.Services;
using OmniSharp.Stdio.Logging;

namespace OmniSharp.Stdio
{
    public class Program
    {
        public static int Main(string[] args) => OmniSharp.Start(() =>
        {
            var application = new OmniSharpStdioCommandLineApplication();
            application.OnExecute(() =>
            {
                var environment = application.CreateEnvironment();
                var writer = new SharedConsoleWriter();
                var plugins = application.CreatePluginAssemblies();
                var configuration = new OmniSharpConfigurationBuilder(environment).Build();
                var serviceProvider = OmniSharpMefBuilder.CreateDefaultServiceProvider(configuration);
                var mefBuilder = new OmniSharpMefBuilder(serviceProvider, environment, writer, new StdioEventEmitter(writer));
                var compositionHost = mefBuilder.Build();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                var cancellation = new CancellationTokenSource();

                using (var program = new Host(Console.In, writer, environment, configuration, serviceProvider, compositionHost, loggerFactory, cancellation))
                {
                    program.Start();
                    cancellation.Token.WaitHandle.WaitOne();
                }

                return 0;
            });
            return application.Execute(args);
        });
    }
}
