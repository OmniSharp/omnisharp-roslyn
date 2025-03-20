using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Models.v1.Completion;
using static OmniSharp.LanguageServerProtocol.Helpers;
using CompletionItem = OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionItem;
using CompletionItemKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionItemKind;
using CompletionItemTag = OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionItemTag;
using CompletionTriggerKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionTriggerKind;
using InsertTextFormat = OmniSharp.Extensions.LanguageServer.Protocol.Models.InsertTextFormat;
using OmnisharpCompletionItem = OmniSharp.Models.v1.Completion.CompletionItem;
using OmnisharpCompletionItemKind = OmniSharp.Models.v1.Completion.CompletionItemKind;
using OmnisharpCompletionItemTag = OmniSharp.Models.v1.Completion.CompletionItemTag;
using OmnisharpCompletionTriggerKind = OmniSharp.Models.v1.Completion.CompletionTriggerKind;
using OmnisharpInsertTextFormat = OmniSharp.Models.v1.Completion.InsertTextFormat;

#nullable enable

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class OmniSharpCompletionHandler : CompletionHandlerBase
    {
        const string AfterInsertCommandName = "csharp.completion.afterInsert";

        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, completionHandler, completionResolveHandler) in handlers
                .OfType<Mef.IRequestHandler<CompletionRequest, CompletionResponse>,
                        Mef.IRequestHandler<CompletionResolveRequest, CompletionResolveResponse>>())
            {
                if (completionHandler != null && completionResolveHandler != null)
                    yield return new OmniSharpCompletionHandler(completionHandler, completionResolveHandler, selector);
            }
        }

        private readonly Mef.IRequestHandler<CompletionRequest, CompletionResponse> _completionHandler;
        private readonly Mef.IRequestHandler<CompletionResolveRequest, CompletionResolveResponse> _completionResolveHandler;
        private readonly TextDocumentSelector _documentSelector;

        public OmniSharpCompletionHandler(
            Mef.IRequestHandler<CompletionRequest, CompletionResponse> completionHandler,
            Mef.IRequestHandler<CompletionResolveRequest, CompletionResolveResponse> completionResolveHandler,
            TextDocumentSelector documentSelector)
        {
            _completionHandler = completionHandler;
            _completionResolveHandler = completionResolveHandler;
            _documentSelector = documentSelector;
        }

        public override async Task<CompletionList> Handle(CompletionParams request, CancellationToken token)
        {
            var omnisharpRequest = new CompletionRequest()
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line),
                CompletionTrigger = Helpers.ConvertEnum<CompletionTriggerKind, OmnisharpCompletionTriggerKind>(request.Context?.TriggerKind ?? CompletionTriggerKind.Invoked),
                TriggerCharacter = request.Context?.TriggerCharacter is { Length: > 0 } str ? str[0] : null
            };

            var omnisharpResponse = await _completionHandler.Handle(omnisharpRequest);

            return new CompletionList(omnisharpResponse.Items.Select(ToLSPCompletionItem), isIncomplete: omnisharpResponse.IsIncomplete);
        }

        public override async Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        {
            var resolveRequest = new CompletionResolveRequest
            {
                Item = ToOmnisharpCompletionItem(request)
            };

            var result = await _completionResolveHandler.Handle(resolveRequest);

            Debug.Assert(result.Item != null);
            return ToLSPCompletionItem(result.Item!);
        }

        protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CompletionRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                ResolveProvider = true,
                TriggerCharacters = new[] { ".", " " },
            };
        }

        private CompletionItem ToLSPCompletionItem(OmnisharpCompletionItem omnisharpCompletionItem)
            => new CompletionItem
            {
                Label = omnisharpCompletionItem.Label,
                Kind = ConvertEnum<OmnisharpCompletionItemKind, CompletionItemKind>(omnisharpCompletionItem.Kind),
                Tags = omnisharpCompletionItem.Tags is { } tags
                    ? Container<CompletionItemTag>.From(tags.Select(ConvertEnum<OmnisharpCompletionItemTag, CompletionItemTag>))
                    : null,
                Detail = omnisharpCompletionItem.Detail,
                Documentation = omnisharpCompletionItem.Documentation is null
                    ? (StringOrMarkupContent?)null
                    : new MarkupContent { Value = omnisharpCompletionItem.Documentation, Kind = MarkupKind.Markdown },
                Preselect = omnisharpCompletionItem.Preselect,
                SortText = omnisharpCompletionItem.SortText,
                FilterText = omnisharpCompletionItem.FilterText,
                InsertTextFormat = ConvertEnum<OmnisharpInsertTextFormat, InsertTextFormat>(omnisharpCompletionItem.InsertTextFormat),
                TextEdit = Helpers.ToTextEdit(omnisharpCompletionItem.TextEdit),
                CommitCharacters = omnisharpCompletionItem.CommitCharacters is { } chars
                    ? Container<string>.From(chars.Select(i => i.ToString()))
                    : null,
                AdditionalTextEdits = omnisharpCompletionItem.AdditionalTextEdits is { } edits
                    ? TextEditContainer.From(edits.Select(e => Helpers.ToTextEdit(e)))
                    : null,
                Data = JToken.FromObject(omnisharpCompletionItem.Data),
                Command = omnisharpCompletionItem.HasAfterInsertStep
                    ? Command.Create(AfterInsertCommandName)
                    : null,
            };

        private OmnisharpCompletionItem ToOmnisharpCompletionItem(CompletionItem completionItem)
            => new OmnisharpCompletionItem
            {
                Label = completionItem.Label,
                Kind = ConvertEnum<CompletionItemKind, OmnisharpCompletionItemKind>(completionItem.Kind),
                Tags = completionItem.Tags?.Select(ConvertEnum<CompletionItemTag, OmnisharpCompletionItemTag>).ToList(),
                Detail = completionItem.Detail,
                Documentation = completionItem.Documentation?.MarkupContent!.Value,
                Preselect = completionItem.Preselect,
                SortText = completionItem.SortText,
                FilterText = completionItem.FilterText,
                InsertTextFormat = ConvertEnum<InsertTextFormat, OmnisharpInsertTextFormat>(completionItem.InsertTextFormat),
                TextEdit = Helpers.FromTextEdit(completionItem.TextEdit!.TextEdit),
                CommitCharacters = completionItem.CommitCharacters?.Select(i => i[0]).ToList(),
                AdditionalTextEdits = completionItem.AdditionalTextEdits?.Select(e => Helpers.FromTextEdit(e)).ToList(),
                Data = completionItem.Data!.ToObject<(long, int)>()
            };
    }
}
