using McMaster.Extensions.CommandLineUtils;
using OmniSharp.Internal;

namespace OmniSharp.Stdio
{
    internal class StdioCommandLineApplication : CommandLineApplication
    {
        private readonly CommandOption _lsp;
        private readonly CommandOption _encoding;

        public StdioCommandLineApplication() : base()
        {
            _lsp = Application.Option("-lsp | --languageserver", "Use Language Server Protocol.", CommandOptionType.NoValue);
            _encoding = Application.Option("-e | --encoding", "Input / output encoding for STDIO protocol.", CommandOptionType.SingleValue);
        }

        public bool Lsp => _lsp.HasValue();

        public string Encoding => _encoding.GetValueOrDefault<string>(null);
    }
}
