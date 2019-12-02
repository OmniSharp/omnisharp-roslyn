using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using OmniSharp.Internal;

namespace OmniSharp
{
    public class CommandLineApplication
    {
        protected readonly McMaster.Extensions.CommandLineUtils.CommandLineApplication Application;
        private readonly CommandOption _hostPid;
        private readonly CommandOption _zeroBasedIndices;
        private readonly CommandOption _plugin;
        private readonly CommandOption _verbose;
        private readonly CommandOption _logLevel;
        private readonly CommandOption _applicationRoot;
        private readonly CommandOption _debug;

        public CommandLineApplication()
        {
            Application = new McMaster.Extensions.CommandLineUtils.CommandLineApplication(throwOnUnexpectedArg: false);
            Application.HelpOption("-? | -h | --help");

            _applicationRoot = Application.Option("-s | --source", "Solution or directory for OmniSharp to point at (defaults to current directory).", CommandOptionType.SingleValue);
            _logLevel = Application.Option("-l | --loglevel", "Level of logging (defaults to 'Information').", CommandOptionType.SingleValue);
            _verbose = Application.Option("-v | --verbose", "Explicitly set 'Debug' log level.", CommandOptionType.NoValue);
            _hostPid = Application.Option("-hpid | --hostPID", "Host process ID.", CommandOptionType.SingleValue);
            _zeroBasedIndices = Application.Option("-z | --zero-based-indices", "Use zero based indices in request/responses (defaults to 'false').", CommandOptionType.NoValue);
            _plugin = Application.Option("-pl | --plugin", "Plugin name(s).", CommandOptionType.MultipleValue);
            _debug = Application.Option("-d | --debug", "Wait for debugger to attach", CommandOptionType.NoValue);
        }

        public int Execute(string[] args)
        {
            // omnisharp.json arguments should not be parsed by the CLI args parser
            // they will contain "=" so we should filter them out
            var extraArgs = new List<string>();
            for (var i = 0; i < args.Length; i++)
            {
                // we are interested in arg with "=" if it's first
                // or not-first but not preceded by "-s" or "--source"
                if (args[i] != null && args[i].Contains("="))
                {
                    if (i == 0)
                    {
                        extraArgs.Add(args[i]);
                    }
                    else if (!args[i - 1].Equals("-s", StringComparison.OrdinalIgnoreCase) && !args[i - 1].Equals("--source", StringComparison.OrdinalIgnoreCase))
                    {
                        extraArgs.Add(args[i]);
                    }
                }
            }

            OtherArgs = extraArgs;
            return Application.Execute(args.Except(OtherArgs).ToArray());
        }

        public void OnExecute(Func<Task<int>> func)
        {
            Application.OnExecute(() =>
            {
                DebugAttach();
                return func();
            });
        }

        public void OnExecute(Func<int> func)
        {
            Application.OnExecute(() =>
            {
                DebugAttach();
                return func();
            });
        }

        public IEnumerable<string> OtherArgs { get; private set; }

        public int HostPid => CommandOptionExtensions.GetValueOrDefault(_hostPid, -1);

        public bool ZeroBasedIndices => _zeroBasedIndices.HasValue();

        public IEnumerable<string> Plugin => _plugin.Values;

        public LogLevel LogLevel => _verbose.HasValue() ? LogLevel.Debug : CommandOptionExtensions.GetValueOrDefault(_logLevel, LogLevel.Information);

        public string ApplicationRoot => CommandOptionExtensions.GetValueOrDefault(_applicationRoot, Directory.GetCurrentDirectory());

        public bool Debug => _debug.HasValue();

        private void DebugAttach()
        {
            if (Debug)
            {
                Console.WriteLine($"Attach debugger to process {Process.GetCurrentProcess().Id} to continue...");
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
}
