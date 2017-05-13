using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using OmniSharp.Internal;

namespace OmniSharp
{
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

        public int HostPid => CommandOptionExtensions.GetValueOrDefault(_hostPid, -1);

        public bool Stdio => _stdio.HasValue();

        public bool Lsp => _stdio.HasValue();

        public bool ZeroBasedIndices => _zeroBasedIndices.HasValue();

        public string Encoding => CommandOptionExtensions.GetValueOrDefault<string>(_encoding, null);

        public IEnumerable<string> Plugin => _plugin.Values;

        public LogLevel LogLevel => _verbose.HasValue() ? LogLevel.Debug : CommandOptionExtensions.GetValueOrDefault(_logLevel, LogLevel.Information);

        public string ApplicationRoot => CommandOptionExtensions.GetValueOrDefault(_applicationRoot, Directory.GetCurrentDirectory());
    }
}