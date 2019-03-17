using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models;
using OmniSharp.Models.FindUsages;
using OmniSharp.Models.MembersTree;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class CodeLensHandler : ICodeLensHandler, ICodeLensResolveHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, membersAsTreeHandler, findUsagesHandler) in handlers
                .OfType<
                    Mef.IRequestHandler<MembersTreeRequest, FileMemberTree>,
                    Mef.IRequestHandler<FindUsagesRequest, QuickFixResponse>>())
            {
                yield return new CodeLensHandler(membersAsTreeHandler, findUsagesHandler, selector);
            }
        }

        private CodeLensCapability _capability;
        private readonly Mef.IRequestHandler<MembersTreeRequest, FileMemberTree> _membersAsTreeHandler;
        private readonly Mef.IRequestHandler<FindUsagesRequest, QuickFixResponse> _findUsagesHandler;
        private readonly DocumentSelector _documentSelector;

        public CodeLensHandler(
            Mef.IRequestHandler<MembersTreeRequest, FileMemberTree> membersAsTreeHandler,
            Mef.IRequestHandler<FindUsagesRequest, QuickFixResponse> findUsagesHandler,
            DocumentSelector documentSelector)
        {
            _membersAsTreeHandler = membersAsTreeHandler;
            _findUsagesHandler = findUsagesHandler;
            _documentSelector = documentSelector;
        }

        public async Task<CodeLensContainer> Handle(CodeLensParams request, CancellationToken token)
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

        public async Task<CodeLens> Handle(CodeLens request, CancellationToken token)
        {
            var omnisharpRequest = new FindUsagesRequest
            {
                FileName = Helpers.FromUri(request.Data.ToObject<Uri>()),
                Column = (int) request.Range.Start.Character,
                Line = (int) request.Range.Start.Line,
                OnlyThisFile = false,
                ExcludeDefinition = true
            };

            var omnisharpResponse = await _findUsagesHandler.Handle(omnisharpRequest);

            var length = omnisharpResponse?.QuickFixes?.Count() ?? 0;

            request.Command = new Command
            {
                Title = length == 1 ? "1 reference" : $"{length} references"
                // TODO: Hook up command.
            };

            return request;
        }


        public CodeLensRegistrationOptions GetRegistrationOptions()
        {
            return new CodeLensRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                ResolveProvider = true
            };
        }

        public void SetCapability(CodeLensCapability capability)
        {
            _capability = capability;
        }

        private static void ToCodeLens(TextDocumentIdentifier textDocument, FileMemberElement node, List<CodeLens> codeLensContainer)
        {
            var codeLens = new CodeLens
            {
                Data = JToken.FromObject(string.IsNullOrEmpty(node.Location.FileName) ?
                    textDocument.Uri :
                    Helpers.ToUri(node.Location.FileName)),
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

        public bool CanResolve(CodeLens value)
        {
            var textDocumentUri = value.Data.ToObject<Uri>();

            return textDocumentUri != null &&
                _documentSelector.IsMatch(new TextDocumentAttributes(textDocumentUri, string.Empty));
        }
    }
}
