using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.LanguageServerProtocol;
using OmniSharp.LanguageServerProtocol.Eventing;
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

                var cancellation = new CancellationTokenSource();

                if (application.Lsp)
                {
                    Configuration.ZeroBasedIndices = true;
                    using (var host = new LanguageServerHost(
                        Console.OpenStandardInput(),
                        Console.OpenStandardOutput(),
                        application,
                        cancellation))
                    {
                        host.Start().Wait();
                        cancellation.Token.WaitHandle.WaitOne();
                    }
                }
                else
                {
                    var input = Console.In;
                    var output = Console.Out;

                    var environment = application.CreateEnvironment();
                    OmniSharp.Configuration.ZeroBasedIndices = application.ZeroBasedIndices;
                    var configuration = new ConfigurationBuilder(environment).Build();
                    var serviceProvider = CompositionHostBuilder.CreateDefaultServiceProvider(configuration);
                    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                    var plugins = application.CreatePluginAssemblies();

                    var writer = new SharedTextWriter(output);
                    var compositionHostBuilder = new CompositionHostBuilder(serviceProvider, environment, new StdioEventEmitter(writer))
                        .WithOmniSharpAssemblies()
                        .WithAssemblies(plugins.AssemblyNames.Select(Assembly.Load).ToArray());
                    using (var host = new Host(input, writer, environment, configuration, serviceProvider, compositionHostBuilder, loggerFactory, cancellation))
                    {
                        host.Start();
                        cancellation.Token.WaitHandle.WaitOne();
                    }
                }

                return 0;
            });

            return application.Execute(args);
        });
    }
}
