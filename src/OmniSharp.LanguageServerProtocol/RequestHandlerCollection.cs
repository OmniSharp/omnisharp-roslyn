using System.Collections;
using System.Collections.Generic;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Mef;

namespace OmniSharp.LanguageServerProtocol
{
    public class RequestHandlerCollection : IEnumerable<IRequestHandler>
    {
        private readonly IEnumerable<IRequestHandler> _handlers;

        public RequestHandlerCollection(string language, IEnumerable<IRequestHandler> handlers, DocumentSelector documentSelector)
        {
            DocumentSelector = documentSelector;
            _handlers = handlers;
            Language = language;
        }

        public string Language { get; }
        public DocumentSelector DocumentSelector { get; }

        public IEnumerator<IRequestHandler> GetEnumerator()
        {
            return _handlers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
