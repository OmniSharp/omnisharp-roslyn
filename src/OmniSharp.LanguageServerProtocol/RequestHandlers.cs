using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
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

        public IEnumerable<(DocumentSelector selector, T handler)> OfType<T>()
            where T : IRequestHandler
        {
            foreach (var group in this)
            {
                yield return (
                    group.DocumentSelector,
                    group.OfType<T>().SingleOrDefault()
                );
            }
        }

        public IEnumerable<(DocumentSelector selector, T handler, T2 handler2)> OfType<T, T2>()
            where T : IRequestHandler
            where T2 : IRequestHandler
        {
            foreach (var group in this)
            {
                yield return (
                    group.DocumentSelector,
                    group.OfType<T>().SingleOrDefault(),
                    group.OfType<T2>().SingleOrDefault()
                );
            }
        }

        public IEnumerable<(DocumentSelector selector, T handler, T2 handler2, T3 handler3)> OfType<T, T2, T3>()
            where T : IRequestHandler
            where T2 : IRequestHandler
            where T3 : IRequestHandler
        {
            foreach (var group in this)
            {
                yield return (
                    group.DocumentSelector,
                    group.OfType<T>().SingleOrDefault(),
                    group.OfType<T2>().SingleOrDefault(),
                    group.OfType<T3>().SingleOrDefault()
                );
            }
        }

        public IEnumerable<(DocumentSelector selector, T handler, T2 handler2, T3 handler3, T4 handler4)> OfType<T, T2, T3, T4>()
            where T : IRequestHandler
            where T2 : IRequestHandler
            where T3 : IRequestHandler
            where T4 : IRequestHandler
        {
            foreach (var group in this)
            {
                yield return (
                    group.DocumentSelector,
                    group.OfType<T>().SingleOrDefault(),
                    group.OfType<T2>().SingleOrDefault(),
                    group.OfType<T3>().SingleOrDefault(),
                    group.OfType<T4>().SingleOrDefault()
                );
            }
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
