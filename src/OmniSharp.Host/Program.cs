using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using OmniSharp.Host.Internal;
using OmniSharp.Host.Loader;
using OmniSharp.Plugins;
using OmniSharp.Services;
using OmniSharp.Stdio;
using OmniSharp.Stdio.Services;
using OmniSharp.Utilities;

namespace OmniSharp
{
    public class Program
    {
        public static int Main2(OmniSharpCommandLineApplication application, string[] args)
        {
            return application.Execute(args);
        }

        public static int Main(string[] args)
        {
            try
            {
#if NET46
                if (PlatformHelper.IsMono)
                {
                    // Mono uses ThreadPool threads for its async/await implementation.
                    // Ensure we have an acceptable lower limit on the threadpool size to avoid deadlocks and ThreadPool starvation.
                    const int MIN_WORKER_THREADS = 8;

                    int currentWorkerThreads, currentCompletionPortThreads;
                    System.Threading.ThreadPool.GetMinThreads(out currentWorkerThreads, out currentCompletionPortThreads);

                    if (currentWorkerThreads < MIN_WORKER_THREADS)
                    {
                        System.Threading.ThreadPool.SetMinThreads(MIN_WORKER_THREADS, currentCompletionPortThreads);
                    }
                }
#endif

                return Run(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
                return 0xbad;
            }
        }

        private static int Run(string[] args)
        {
            Console.WriteLine($"OmniSharp: {string.Join(" ", args)}");

            // omnisharp.json arguments should not be parsed by the CLI args parser
            // they will contain "=" so we should filter them out
            var omnisharpJsonArgs = args.Where(x => x.Contains("="));

            var omnisharpApp = new CommandLineApplication(throwOnUnexpectedArg: false);
            omnisharpApp.HelpOption("-? | -h | --help");

            var applicationRootOption = omnisharpApp.Option("-s | --source", "Solution or directory for OmniSharp to point at (defaults to current directory).", CommandOptionType.SingleValue);
            var logLevelOption = omnisharpApp.Option("-l | --loglevel", "Level of logging (defaults to 'Information').", CommandOptionType.SingleValue);
            var verboseOption = omnisharpApp.Option("-v | --verbose", "Explicitly set 'Debug' log level.", CommandOptionType.NoValue);
            var hostPidOption = omnisharpApp.Option("-hpid | --hostPID", "Host process ID.", CommandOptionType.SingleValue);
            var stdioOption = omnisharpApp.Option("-stdio | --stdio", "Use STDIO over HTTP as OmniSharp commincation protocol.", CommandOptionType.NoValue);
            var zeroBasedIndicesOption = omnisharpApp.Option("-z | --zero-based-indices", "Use zero based indices in request/responses (defaults to 'false').", CommandOptionType.NoValue);
            var encodingOption = omnisharpApp.Option("-e | --encoding", "Input / output encoding for STDIO protocol.", CommandOptionType.SingleValue);
            var pluginOption = omnisharpApp.Option("-pl | --plugin", "Plugin name(s).", CommandOptionType.MultipleValue);
    
            // TODO: Generalize this so that each "host" can define their own options
            var portOption = omnisharpApp.Option("-p | --port", "OmniSharp port (defaults to 2000).", CommandOptionType.SingleValue);
            var serverInterfaceOption = omnisharpApp.Option("-i | --interface", "Server interface address (defaults to 'localhost').", CommandOptionType.SingleValue);

            omnisharpApp.OnExecute(() =>
            {
                var applicationRoot = applicationRootOption.GetValueOrDefault(Directory.GetCurrentDirectory());
                var logLevel = verboseOption.HasValue() ? LogLevel.Debug : logLevelOption.GetValueOrDefault(LogLevel.Information);
                var hostPid = hostPidOption.GetValueOrDefault(-1);
                var transportType = stdioOption.HasValue() ? TransportType.Stdio : TransportType.Http;
                var encodingString = encodingOption.GetValueOrDefault<string>(null);
                var plugins = pluginOption.Values;
                var otherArgs = omnisharpApp.RemainingArguments.Union(omnisharpJsonArgs).Distinct();
                Configuration.ZeroBasedIndices = zeroBasedIndicesOption.HasValue();

                // TODO: Generalize this so that each "host" can define their own options
                var serverPort = portOption.GetValueOrDefault(2000);
                var serverInterface = serverInterfaceOption.GetValueOrDefault("localhost");

                var env = new OmniSharpEnvironment(applicationRoot, hostPid, logLevel, transportType, otherArgs.ToArray());

                // If the --encoding switch was specified, we need to set the InputEncoding and OutputEncoding before
                // constructing the SharedConsoleWriter. Otherwise, it might be created with the wrong encoding since
                // it wraps around Console.Out, which gets recreated when OutputEncoding is set.
                if (transportType == TransportType.Stdio && encodingString != null)
                {
                    var encoding = Encoding.GetEncoding(encodingString);
                    Console.InputEncoding = encoding;
                    Console.OutputEncoding = encoding;
                }

                var writer = new SharedConsoleWriter();

                var builder = new WebHostBuilder()
                    .UseConfiguration(config.Build())
                    .UseEnvironment("OmniSharp")
                    .ConfigureServices(serviceCollection =>
                    {
                        serviceCollection.AddSingleton<IOmniSharpEnvironment>(env);
                        serviceCollection.AddSingleton<ISharedTextWriter>(writer);
                        serviceCollection.AddSingleton<PluginAssemblies>(new PluginAssemblies(plugins));
                        serviceCollection.AddSingleton<IAssemblyLoader, AssemblyLoader>();
                    })
                    .UseStartup(typeof(Startup));

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

                    if (hostPid != -1)
                    {
                        try
                        {
                            var hostProcess = Process.GetProcessById(hostPid);
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

                return 0;
            });

            return omnisharpApp.Execute(args.Except(omnisharpJsonArgs).ToArray());
        }
    }

    public class OmniSharpCommandLineApplication
    {
        protected readonly CommandLineApplication Application;
        private readonly CommandOption _hostPid;
        private readonly CommandOption _stdio;
        private readonly CommandOption _zeroBasedIndices;
        private readonly CommandOption _encoding;
        private readonly CommandOption _plugin;
        private readonly CommandOption _verbose;
        private readonly CommandOption _logLevel;
        private readonly CommandOption _applicationRoot;

        public OmniSharpCommandLineApplication()
        {
            Application = new CommandLineApplication(throwOnUnexpectedArg: false);
            Application.HelpOption("-? | -h | --help");

            _applicationRoot = Application.Option("-s | --source", "Solution or directory for OmniSharp to point at (defaults to current directory).", CommandOptionType.SingleValue);
            _logLevel = Application.Option("-l | --loglevel", "Level of logging (defaults to 'Information').", CommandOptionType.SingleValue);
            _verbose = Application.Option("-v | --verbose", "Explicitly set 'Debug' log level.", CommandOptionType.NoValue);
            _hostPid = Application.Option("-hpid | --hostPID", "Host process ID.", CommandOptionType.SingleValue);
            _stdio = Application.Option("-stdio | --stdio", "Use STDIO over HTTP as OmniSharp commincation protocol.", CommandOptionType.NoValue);
            _stdio = Application.Option("-lsp | --lsp", "Use Language Server Protocol.", CommandOptionType.NoValue);
            _zeroBasedIndices = Application.Option("-z | --zero-based-indices", "Use zero based indices in request/responses (defaults to 'false').", CommandOptionType.NoValue);
            _encoding = Application.Option("-e | --encoding", "Input / output encoding for STDIO protocol.", CommandOptionType.SingleValue);
            _plugin = Application.Option("-pl | --plugin", "Plugin name(s).", CommandOptionType.MultipleValue);
        }

        public int Execute(IEnumerable<string> args)
        {
            // omnisharp.json arguments should not be parsed by the CLI args parser
            // they will contain "=" so we should filter them out
            OtherArgs = args.Where(x => x.Contains("="));

            return Application.Execute(args.Except(OtherArgs).ToArray());
        }

        public void OnExecute(Func<Task<int>> func)
        {
            Application.OnExecute(func);
        }

        public void OnExecute(Func<int> func)
        {
            Application.OnExecute(func);
        }

        public IEnumerable<string> OtherArgs { get; private set; }

        public int HostPid => _hostPid.GetValueOrDefault(-1);

        public bool Stdio => _stdio.HasValue();

        public bool Lsp => _stdio.HasValue();

        public bool ZeroBasedIndices => _zeroBasedIndices.HasValue();

        public string Encoding => _encoding.GetValueOrDefault<string>(null);

        public IEnumerable<string> Plugin => _plugin.Values;

        public LogLevel LogLevel => _verbose.HasValue() ? LogLevel.Debug : _logLevel.GetValueOrDefault(LogLevel.Information);

        public string ApplicationRoot => _applicationRoot.GetValueOrDefault(Directory.GetCurrentDirectory());
    }

    public static class OmniSharpCommandLineApplicationExtensions {
        public static OmniSharpEnvironment CreateEnvironment(this OmniSharpCommandLineApplication application)
        {
            return new OmniSharpEnvironment(
                application.ApplicationRoot, 
                application.HostPid, 
                application.LogLevel,
                application.OtherArgs.ToArray());

        }

        public static PluginAssemblies CreatePluginAssemblies(this OmniSharpCommandLineApplication application)
        {
            return new PluginAssemblies(application.Plugin);
        }
    }
}
