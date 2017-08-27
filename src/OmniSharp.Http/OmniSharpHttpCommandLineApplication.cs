using Microsoft.Extensions.CommandLineUtils;
using OmniSharp.Internal;

namespace OmniSharp.Http
{
    public class OmniSharpHttpCommandLineApplication : OmniSharpCommandLineApplication
    {
        private readonly CommandOption _serverInterface;
        private readonly CommandOption _port;

        public OmniSharpHttpCommandLineApplication() : base()
        {
            _port = Application.Option("-p | --port", "OmniSharp port (defaults to 2000).", CommandOptionType.SingleValue);
            _serverInterface = Application.Option("-i | --interface", "Server interface address (defaults to 'localhost').", CommandOptionType.SingleValue);
        }


        public int Port => _port.GetValueOrDefault(2000);
        public string Interface => _serverInterface.GetValueOrDefault("localhost");
    }
}