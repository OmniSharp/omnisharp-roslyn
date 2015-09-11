using System;
using System.Threading.Tasks;
using OmniSharp.Plugins;

namespace OmniSharp.Middleware.Endpoint.Exports
{
    class PluginExportHandler : ExportHandler
    {
        private readonly string _endpoint;
        private readonly Plugin _plugin;
        private readonly Type _responseType;

        public PluginExportHandler(string endpoint, Plugin plugin, Type responseType) : base(plugin.Config.Language)
        {
            _endpoint = endpoint;
            _plugin = plugin;
            _responseType = responseType;
        }

        public override Task<object> Handle(object request)
        {
            return _plugin.Handle(_endpoint, request, _responseType);
        }
    }
}
