using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models;
using OmniSharp.Models.FindUsages;
using OmniSharp.Models.MembersTree;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpCodeLensHandler : CodeLensHandlerBase
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, membersAsTreeHandler, findUsagesHandler) in handlers
                .OfType<
                    Mef.IRequestHandler<MembersTreeRequest, FileMemberTree>,
                    Mef.IRequestHandler<FindUsagesRequest, QuickFixResponse>>())
            {
                yield return new OmniSharpCodeLensHandler(membersAsTreeHandler, findUsagesHandler, selector);
            }
        }

        private readonly Mef.IRequestHandler<MembersTreeRequest, FileMemberTree> _membersAsTreeHandler;
        private readonly Mef.IRequestHandler<FindUsagesRequest, QuickFixResponse> _findUsagesHandler;
        private readonly TextDocumentSelector _documentSelector;

        public OmniSharpCodeLensHandler(
            Mef.IRequestHandler<MembersTreeRequest, FileMemberTree> membersAsTreeHandler,
            Mef.IRequestHandler<FindUsagesRequest, QuickFixResponse> findUsagesHandler,
            TextDocumentSelector documentSelector)
        {
            _membersAsTreeHandler = membersAsTreeHandler;
            _findUsagesHandler = findUsagesHandler;
            _documentSelector = documentSelector;
        }

        public override async Task<CodeLensContainer> Handle(CodeLensParams request, CancellationToken token)
        {
            var omnisharpRequest = new MembersTreeRequest()
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
            };

            var omnisharpResponse = await _membersAsTreeHandler.Handle(omnisharpRequest);
            var codeLenseContainer = new List<CodeLens>();

            foreach (var node in omnisharpResponse.TopLevelTypeDefinitions)
            {
                ToCodeLens(request.TextDocument, node, codeLenseContainer);
            }

            return codeLenseContainer;
        }

        public override async Task<CodeLens> Handle(CodeLens request, CancellationToken token)
        {
            var omnisharpRequest = new FindUsagesRequest
            {
                FileName = Helpers.FromUri(request.Data.ToObject<Uri>()),
                Column = (int)request.Range.Start.Character,
                Line = (int)request.Range.Start.Line,
                OnlyThisFile = false,
                ExcludeDefinition = true
            };

            var omnisharpResponse = await _findUsagesHandler.Handle(omnisharpRequest);

            var length = omnisharpResponse?.QuickFixes?.Count() ?? 0;

            var jsonCamelCaseContract = new JsonSerializer
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            request = request with
            {
                Command = new Command
                {
                    Title = length == 1 ? "1 reference" : $"{length} references",
                    Name = "omnisharp/client/findReferences",
                    Arguments = new JArray(
                    new[]
                    {
                        JObject.FromObject(
                            new Location
                            {
                                Uri = request.Data.ToObject<Uri>()!,
                                Range = request.Range,
                            },
                            jsonCamelCaseContract)
                    }),
                }
            };

            return request;
        }

        private static void ToCodeLens(TextDocumentIdentifier textDocument, FileMemberElement node,
            List<CodeLens> codeLensContainer)
        {
            var codeLens = new CodeLens
            {
                Data = JToken.FromObject(string.IsNullOrEmpty(node.Location.FileName)
                    ? textDocument.Uri
                    : Helpers.ToUri(node.Location.FileName)),
                Range = node.Location.ToRange()
            };

            codeLensContainer.Add(codeLens);

            if (node.ChildNodes != null)
            {
                foreach (var childNode in node.ChildNodes)
                {
                    ToCodeLens(textDocument, childNode, codeLensContainer);
                }
            }
        }

        protected override CodeLensRegistrationOptions CreateRegistrationOptions(CodeLensCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CodeLensRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                ResolveProvider = true
            };
        }
    }
}
