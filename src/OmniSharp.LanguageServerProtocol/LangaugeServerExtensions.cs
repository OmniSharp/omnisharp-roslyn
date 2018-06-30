using System.Collections.Generic;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Extensions.LanguageServer.Server;

namespace OmniSharp.LanguageServerProtocol
{
    public static class LangaugeServerExtensions
    {
        public static ILanguageServer AddHandlers(this ILanguageServer langaugeServer, IEnumerable<IJsonRpcHandler> handlers)
        {
            langaugeServer.AddHandlers(handlers);
            return langaugeServer;
        }
    }
}
