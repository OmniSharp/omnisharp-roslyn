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
using static OmniSharp.LanguageServerProtocol.Helpers;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal class OmniSharpSemanticTokensHandler : SemanticTokensHandlerBase
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers.OfType<Mef.IRequestHandler<SemanticHighlightRequest, SemanticHighlightResponse>>())
                if (handler != null)
                    yield return new OmniSharpSemanticTokensHandler(handler, selector);
        }

        internal static readonly ImmutableDictionary<SemanticHighlightClassification, SemanticTokenType> _coreTokenMap =
            new Dictionary<SemanticHighlightClassification, SemanticTokenType>()
            {
                [SemanticHighlightClassification.Comment] = SemanticTokenType.Comment,
                [SemanticHighlightClassification.Keyword] = SemanticTokenType.Keyword,
                [SemanticHighlightClassification.NumericLiteral] = SemanticTokenType.Number,
                [SemanticHighlightClassification.Operator] = SemanticTokenType.Operator,
                [SemanticHighlightClassification.StringLiteral] = SemanticTokenType.String,
                [SemanticHighlightClassification.ClassName] = SemanticTokenType.Class,
                [SemanticHighlightClassification.StructName] = SemanticTokenType.Struct,
                [SemanticHighlightClassification.NamespaceName] = SemanticTokenType.Namespace,
                [SemanticHighlightClassification.EnumName] = SemanticTokenType.Enum,
                [SemanticHighlightClassification.InterfaceName] = SemanticTokenType.Interface,
                [SemanticHighlightClassification.TypeParameterName] = SemanticTokenType.TypeParameter,
                [SemanticHighlightClassification.ParameterName] = SemanticTokenType.Parameter,
                [SemanticHighlightClassification.LocalName] = SemanticTokenType.Variable,
                [SemanticHighlightClassification.PropertyName] = SemanticTokenType.Property,
                [SemanticHighlightClassification.MethodName] = SemanticTokenType.Method,
                [SemanticHighlightClassification.EnumMemberName] = SemanticTokenType.EnumMember,
                [SemanticHighlightClassification.EventName] = SemanticTokenType.Event,
                [SemanticHighlightClassification.PreprocessorKeyword] = SemanticTokenType.Macro,
                [SemanticHighlightClassification.LabelName] = SemanticTokenType.Label,
            }.ToImmutableDictionary();

        private readonly Mef.IRequestHandler<SemanticHighlightRequest, SemanticHighlightResponse> _definitionHandler;
        private readonly TextDocumentSelector _documentSelector;

        private static string MakeLSPCompatibleString(string str)
            => char.ToLower(str[0]) + str.Substring(1);

        private static readonly ImmutableDictionary<SemanticHighlightClassification, SemanticTokenType> _tokenTypes
            = System.Enum.GetValues(typeof(SemanticHighlightClassification))
                .Cast<SemanticHighlightClassification>()
                .ToImmutableDictionary(value => value,
                    // Use Core LSP token types where possible
                    value => _coreTokenMap.ContainsKey(value)
                        ? _coreTokenMap[value]
                        : new SemanticTokenType(MakeLSPCompatibleString(value.ToString())));

        private static readonly ImmutableDictionary<SemanticHighlightModifier, SemanticTokenModifier> _tokenModifiers
            = System.Enum.GetValues(typeof(SemanticHighlightModifier))
                .Cast<SemanticHighlightModifier>()
                .ToImmutableDictionary(value => value, value => new SemanticTokenModifier(MakeLSPCompatibleString(value.ToString())));

        private readonly SemanticTokensLegend _legend = new()
        {
            TokenTypes = new Container<SemanticTokenType>(_tokenTypes.Values),
            TokenModifiers = new Container<SemanticTokenModifier>(_tokenModifiers.Values),
        };

        public OmniSharpSemanticTokensHandler(Mef.IRequestHandler<SemanticHighlightRequest, SemanticHighlightResponse> definitionHandler, TextDocumentSelector documentSelector)
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
