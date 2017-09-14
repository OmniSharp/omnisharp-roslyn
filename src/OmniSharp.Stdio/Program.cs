using System;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Services;
using OmniSharp.Stdio.Eventing;

namespace OmniSharp.Stdio
{
    class Program
    {
        static int Main(string[] args) => HostHelpers.Start(() =>
        {
            var application = new StdioCommandLineApplication();
            application.OnExecute(() =>
            {
                // If an encoding was specified, be sure to set the Console with it before we access the input/output streams.
                // Otherwise, the streams will be created with the default encoding.
                if (application.Encoding != null)
                {
                    var encoding = Encoding.GetEncoding(application.Encoding);
                    Console.InputEncoding = encoding;
                    Console.OutputEncoding = encoding;
                }

                var input = Console.In;
                var output = Console.Out;

                var environment = application.CreateEnvironment();
                var writer = new SharedTextWriter(output);
                var plugins = application.CreatePluginAssemblies();
                var configuration = new ConfigurationBuilder(environment).Build();
                var serviceProvider = MefBuilder.CreateDefaultServiceProvider(configuration);
                var mefBuilder = new MefBuilder(serviceProvider, environment, writer, new StdioEventEmitter(writer));
                var compositionHost = mefBuilder.Build();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                var cancellation = new CancellationTokenSource();

                using (var host = new Host(input, writer, environment, configuration, serviceProvider, compositionHost, loggerFactory, cancellation))
                {
                    host.Start();
                    cancellation.Token.WaitHandle.WaitOne();
                }

                return 0;
            });

            return application.Execute(args);
        });
    }
}
