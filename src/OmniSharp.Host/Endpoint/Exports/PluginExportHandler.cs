using System.Threading.Tasks;
using OmniSharp.Plugins;

namespace OmniSharp.Endpoint.Exports
{
    class PluginExportHandler<TRequest, TResponse> : ExportHandler<TRequest, TResponse>
    {
        private readonly string _endpoint;
        private readonly Plugin _plugin;

        public PluginExportHandler(string endpoint, Plugin plugin) : base(plugin.Config.Language)
        {
            _endpoint = endpoint;
            _plugin = plugin;
        }

        public override int CompareTo(ExportHandler<TRequest, TResponse> other)
        {
            var otherPlugin = other as PluginExportHandler<TRequest, TResponse>;
            if (otherPlugin == null)
            {
                return 1;
            }

            return _plugin.Key.CompareTo(otherPlugin._plugin.Key);
        }

        public override Task<TResponse> Handle(TRequest request)
        {
            return _plugin.Handle<TRequest, TResponse>(_endpoint, request);
        }
    }
}
