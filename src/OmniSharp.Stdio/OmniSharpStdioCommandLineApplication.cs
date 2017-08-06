using Microsoft.Extensions.CommandLineUtils;
using OmniSharp.Internal;

namespace OmniSharp.Stdio
{
    public class OmniSharpStdioCommandLineApplication : OmniSharpCommandLineApplication
    {
        private readonly CommandOption _stdio;
        private readonly CommandOption _lsp;

        public OmniSharpStdioCommandLineApplication() : base()
        {
            _stdio = Application.Option("-stdio | --stdio", "Use STDIO over HTTP as OmniSharp communication protocol.", CommandOptionType.NoValue);
            _lsp = Application.Option("-lsp | --lsp", "Use Language Server Protocol.", CommandOptionType.NoValue);
        }

        public bool Stdio => _stdio.GetValueOrDefault(true);

        public bool Lsp => _lsp.GetValueOrDefault(false);
    }
}
