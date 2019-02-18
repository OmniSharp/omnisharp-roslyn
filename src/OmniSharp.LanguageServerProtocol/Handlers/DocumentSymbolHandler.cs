using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.MembersTree;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class DocumentSymbolHandler : IDocumentSymbolHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<MembersTreeRequest, FileMemberTree>>())
                if (handler != null)
                    yield return new DocumentSymbolHandler(handler, selector);
        }

        private DocumentSymbolCapability _capability;
        private readonly Mef.IRequestHandler<MembersTreeRequest, FileMemberTree> _membersAsTreeHandler;
        private readonly DocumentSelector _documentSelector;

        private static readonly IDictionary<string, SymbolKind> Kinds = new Dictionary<string, SymbolKind>
        {
            {"NamespaceDeclaration", SymbolKind.Namespace},
            {"ClassDeclaration", SymbolKind.Class},
            {"FieldDeclaration", SymbolKind.Field},
            {"PropertyDeclaration", SymbolKind.Property},
            {"EventFieldDeclaration", SymbolKind.Property},
            {"MethodDeclaration", SymbolKind.Method},
            {"EnumDeclaration", SymbolKind.Enum},
            {"StructDeclaration", SymbolKind.Enum},
            {"EnumMemberDeclaration", SymbolKind.Property},
            {"InterfaceDeclaration", SymbolKind.Interface},
            {"VariableDeclaration", SymbolKind.Variable}
        };

        public DocumentSymbolHandler(Mef.IRequestHandler<MembersTreeRequest, FileMemberTree> membersAsTreeHandler, DocumentSelector documentSelector)
        {
            _membersAsTreeHandler = membersAsTreeHandler;
            _documentSelector = documentSelector;
        }

        public async Task<DocumentSymbolInformationContainer> Handle(DocumentSymbolParams request, CancellationToken token)
        {
            var omnisharpRequest = new MembersTreeRequest()
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
            };

            var omnisharpResponse = await _membersAsTreeHandler.Handle(omnisharpRequest);
            var symbolInformationContainer = new List<DocumentSymbolInformation>();

            foreach (var node in omnisharpResponse.TopLevelTypeDefinitions)
            {
                ToDocumentSymbol(node, symbolInformationContainer);
            }

            return symbolInformationContainer;
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector
            };
        }

        public void SetCapability(DocumentSymbolCapability capability)
        {
            _capability = capability;
        }

        private static void ToDocumentSymbol(FileMemberElement node, List<DocumentSymbolInformation> symbolInformationContainer, string containerName = null)
        {
            var symbolInformation = new DocumentSymbolInformation
            {
                Name = node.Location.Text,
                Kind = Kinds[node.Kind],
                Location = new Location
                {
                    Uri = Helpers.ToUri(node.Location.FileName),
                    Range = node.Location.ToRange()
                },
                ContainerName = containerName
            };

            if (node.ChildNodes != null)
            {
                foreach (var childNode in node.ChildNodes)
                {
                    ToDocumentSymbol(childNode, symbolInformationContainer, symbolInformation.Name);
                }
            }
            symbolInformationContainer.Add(symbolInformation);
        }
    }
}
