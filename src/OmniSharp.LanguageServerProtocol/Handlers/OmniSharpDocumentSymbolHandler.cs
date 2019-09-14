using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.MembersTree;
using OmniSharp.Models.V2.CodeStructure;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpDocumentSymbolHandler : DocumentSymbolHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<CodeStructureRequest, CodeStructureResponse>>())
                if (handler != null)
                    yield return new OmniSharpDocumentSymbolHandler(handler, selector);
        }

        private readonly Mef.IRequestHandler<CodeStructureRequest, CodeStructureResponse> _codeStructureHandler;

        private static readonly IDictionary<string, SymbolKind> Kinds = new Dictionary<string, SymbolKind>
        {
            { OmniSharp.Models.V2.SymbolKinds.Class, SymbolKind.Class },
            { OmniSharp.Models.V2.SymbolKinds.Delegate, SymbolKind.Class },
            { OmniSharp.Models.V2.SymbolKinds.Enum, SymbolKind.Enum },
            { OmniSharp.Models.V2.SymbolKinds.Interface, SymbolKind.Interface },
            { OmniSharp.Models.V2.SymbolKinds.Struct, SymbolKind.Struct },
            { OmniSharp.Models.V2.SymbolKinds.Constant, SymbolKind.Constant },
            { OmniSharp.Models.V2.SymbolKinds.Destructor, SymbolKind.Method },
            { OmniSharp.Models.V2.SymbolKinds.EnumMember, SymbolKind.EnumMember },
            { OmniSharp.Models.V2.SymbolKinds.Event, SymbolKind.Event },
            { OmniSharp.Models.V2.SymbolKinds.Field, SymbolKind.Field },
            { OmniSharp.Models.V2.SymbolKinds.Indexer, SymbolKind.Property },
            { OmniSharp.Models.V2.SymbolKinds.Method, SymbolKind.Method },
            { OmniSharp.Models.V2.SymbolKinds.Operator, SymbolKind.Operator },
            { OmniSharp.Models.V2.SymbolKinds.Property, SymbolKind.Property },
            { OmniSharp.Models.V2.SymbolKinds.Namespace, SymbolKind.Namespace },
            { OmniSharp.Models.V2.SymbolKinds.Unknown, SymbolKind.Class },
        };

        public OmniSharpDocumentSymbolHandler(Mef.IRequestHandler<CodeStructureRequest, CodeStructureResponse> codeStructureHandler, DocumentSelector documentSelector)
            : base(new TextDocumentRegistrationOptions()
            {
                DocumentSelector = documentSelector
            })
        {
            _codeStructureHandler = codeStructureHandler;
        }

        public async override Task<SymbolInformationOrDocumentSymbolContainer> Handle(DocumentSymbolParams request, CancellationToken token)
        {
            var omnisharpRequest = new CodeStructureRequest()
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
            };

            var omnisharpResponse = await _codeStructureHandler.Handle(omnisharpRequest);

            return omnisharpResponse.Elements?.Select(ToDocumentSymbolInformationOrDocumentSymbol).ToArray() ??
                Array.Empty<SymbolInformationOrDocumentSymbol>();
        }

        private static SymbolInformationOrDocumentSymbol ToDocumentSymbolInformationOrDocumentSymbol(CodeElement node)
        {
            return new SymbolInformationOrDocumentSymbol(ToDocumentSymbol(node));
        }

        private static DocumentSymbol ToDocumentSymbol(CodeElement node)
        {
            return new DocumentSymbol
            {
                Name = node.Name,
                Kind = Kinds.ContainsKey(node.Kind) ? Kinds[node.Kind] : SymbolKind.Class,
                Range = Helpers.ToRange(node.Ranges[OmniSharp.Models.V2.SymbolRangeNames.Full]),
                SelectionRange = Helpers.ToRange(node.Ranges[OmniSharp.Models.V2.SymbolRangeNames.Name]),
                Children = new Container<DocumentSymbol>(node.Children?.Select(ToDocumentSymbol) ?? Enumerable.Empty<DocumentSymbol>())
            };
        }
    }
}
