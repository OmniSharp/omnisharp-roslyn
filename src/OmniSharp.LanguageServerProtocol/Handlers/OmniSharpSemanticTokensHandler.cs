using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Models.SemanticHighlight;
using OmniSharp.Roslyn.CSharp.Services.SemanticHighlight;
using static OmniSharp.LanguageServerProtocol.Helpers;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class OmniSharpSemanticTokensHandler : SemanticTokensHandlerBase
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers.OfType<Mef.IRequestHandler<SemanticHighlightRequest, SemanticHighlightResponse>>())
                if (handler != null)
                    yield return new OmniSharpSemanticTokensHandler(handler, selector);
        }

        private readonly Mef.IRequestHandler<SemanticHighlightRequest, SemanticHighlightResponse> _definitionHandler;
        private readonly DocumentSelector _documentSelector;

        private static readonly ImmutableDictionary<SemanticHighlightClassification, SemanticTokenType> _tokenTypes
            = SemanticHighlightService._classificationMap
                .OrderBy(kvp => kvp.Value)
                .Aggregate(
                    new Dictionary<SemanticHighlightClassification, SemanticTokenType>(),
                    (dictionary, kvp) =>
                    {
                        if (!dictionary.ContainsKey(kvp.Value))
                            dictionary.Add(kvp.Value, new SemanticTokenType(kvp.Key));
                        return dictionary;
                    })
                    .ToImmutableDictionary();

        private static readonly ImmutableDictionary<SemanticHighlightModifier, SemanticTokenModifier> _tokenModifiers
            = SemanticHighlightService._modifierMap
                .OrderBy(kvp => kvp.Value)
                .Aggregate(
                    new Dictionary<SemanticHighlightModifier, SemanticTokenModifier>(),
                    (dictionary, kvp) =>
                    {
                        if (!dictionary.ContainsKey(kvp.Value))
                            dictionary.Add(kvp.Value, new SemanticTokenModifier(kvp.Key));
                        return dictionary;
                    })
                    .ToImmutableDictionary();

        private readonly SemanticTokensLegend _legend = new()
        {
            TokenTypes = new Container<SemanticTokenType>(_tokenTypes.Values),
            TokenModifiers = new Container<SemanticTokenModifier>(_tokenModifiers.Values),
        };

        public OmniSharpSemanticTokensHandler(Mef.IRequestHandler<SemanticHighlightRequest, SemanticHighlightResponse> definitionHandler, DocumentSelector documentSelector)
        {
            _definitionHandler = definitionHandler;
            _documentSelector = documentSelector;
        }

        protected override async Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier, CancellationToken cancellationToken)
        {
            var omnisharpRequest = new SemanticHighlightRequest()
            {
                FileName = FromUri(identifier.TextDocument.Uri)
            };

            var omnisharpResponse = await _definitionHandler.Handle(omnisharpRequest);

            if (omnisharpResponse.Spans is null)
            {
                return;
            }

            foreach (var span in omnisharpResponse.Spans)
            {
                var range = new Range(span.StartLine, span.StartColumn, span.EndLine, span.EndColumn);
                builder.Push(range, _tokenTypes[span.Type], span.Modifiers.Select(modifier => _tokenModifiers[modifier]));
            }
        }

        protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticTokensDocument(_legend));
        }

        protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
        {
            return new SemanticTokensRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                Full = new SemanticTokensCapabilityRequestFull
                {
                    Delta = false
                },
                Range = true,
                Legend = _legend
            };
        }
    }
}
