using McMaster.Extensions.CommandLineUtils;
using OmniSharp.Internal;

namespace OmniSharp.Http
{
    internal class HttpCommandLineApplication : CommandLineApplication
    {
        private readonly CommandOption _serverInterface;
        private readonly CommandOption _port;

        public HttpCommandLineApplication() : base()
        {
            _port = Application.Option("-p | --port", "OmniSharp port (defaults to 2000).", CommandOptionType.SingleValue);
            _serverInterface = Application.Option("-i | --interface", "Server interface address (defaults to 'localhost').", CommandOptionType.SingleValue);
        }

        public int Port => _port.GetValueOrDefault(2000);
        public string Interface => _serverInterface.GetValueOrDefault("localhost");
    }
}
