using System;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.LanguageServerProtocol;
using OmniSharp.Options;
using OmniSharp.Services;
using OmniSharp.Stdio.Eventing;
using OmniSharp.Stdio.Logging;

namespace OmniSharp.Stdio.Driver
{
    internal class Program
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
                    Configuration.ZeroBasedIndices = application.ZeroBasedIndices;
                    var configurationResult = new ConfigurationBuilder(environment).Build();
                    var writer = new SharedTextWriter(output);
                    var serviceProvider = CompositionHostBuilder.CreateDefaultServiceProvider(environment, configurationResult.Configuration, new StdioEventEmitter(writer),
                        configureLogging: builder => builder.AddStdio(writer));

                    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                    var assemblyLoader = serviceProvider.GetRequiredService<IAssemblyLoader>();

                    var options = serviceProvider.GetRequiredService<IOptionsMonitor<OmniSharpOptions>>();
                    var plugins = application.CreatePluginAssemblies(options.CurrentValue, environment);

                    var logger = loggerFactory.CreateLogger<Program>();
                    if (configurationResult.HasError())
                    {
                        logger.LogError(configurationResult.Exception, "There was an error when reading the OmniSharp configuration, starting with the default options.");
                    }
                    var compositionHostBuilder = new CompositionHostBuilder(serviceProvider)
                        .WithOmniSharpAssemblies()
                        .WithAssemblies(assemblyLoader.LoadByAssemblyNameOrPath(logger, plugins.AssemblyNames).ToArray());

                    using (var host = new Host(input, writer, environment, serviceProvider, compositionHostBuilder, loggerFactory, cancellation))
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
