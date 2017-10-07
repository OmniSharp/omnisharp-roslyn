using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OmniSharp.Extensions.LanguageServer.Models;
using OmniSharp.Mef;

namespace OmniSharp.LanguageServerProtocol
{
    public class RequestHandlers : IEnumerable<RequestHandlerCollection>
    {
        private readonly IEnumerable<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> _requestHandlers;
        private readonly IEnumerable<(string language, DocumentSelector selector)> _documentSelectors;

        public RequestHandlers(
            IEnumerable<Lazy<IRequestHandler, OmniSharpRequestHandlerMetadata>> requestHandlers,
            IEnumerable<(string language, DocumentSelector selector)> documentSelectors)
        {
            _requestHandlers = requestHandlers;
            _documentSelectors = documentSelectors;
        }

        public IEnumerator<RequestHandlerCollection> GetEnumerator()
        {
            return _documentSelectors
                .Select(documentSelector => new RequestHandlerCollection(
                    documentSelector.language,
                    _requestHandlers.Where(z => z.Metadata.Language == documentSelector.language).Select(z => z.Value),
                    documentSelector.selector)
                )
                .GetEnumerator();
        }

        public IEnumerable<IRequestHandler> GetAll()
        {
            return _requestHandlers.Select(z => z.Value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}