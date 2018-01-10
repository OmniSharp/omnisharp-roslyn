using System.Collections.Generic;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer;

namespace OmniSharp.LanguageServerProtocol
{
    public static class LangaugeServerExtensions
    {
        public static LanguageServer AddHandlers(this LanguageServer langaugeServer, IEnumerable<IJsonRpcHandler> handlers)
        {
            foreach (var handler in handlers)
                langaugeServer.AddHandler(handler);
            return langaugeServer;
        }
    }
}