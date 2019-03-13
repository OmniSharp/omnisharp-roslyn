using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.AutoComplete;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class CompletionHandler : ICompletionHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {

            foreach (var (selector, handler) in handlers
                .OfType<Mef.IRequestHandler<AutoCompleteRequest, IEnumerable<AutoCompleteResponse>>>())
                if (handler != null)
                    yield return new CompletionHandler(handler, selector);
        }

        private CompletionCapability _capability;
        private readonly Mef.IRequestHandler<AutoCompleteRequest, IEnumerable<AutoCompleteResponse>> _autoCompleteHandler;
        private readonly DocumentSelector _documentSelector;

        private static readonly IDictionary<string, CompletionItemKind> _kind = new Dictionary<string, CompletionItemKind>{
            // types
            { "Class",  CompletionItemKind.Class },
            { "Delegate", CompletionItemKind.Class }, // need a better option for this.
            { "Enum", CompletionItemKind.Enum },
            { "Interface", CompletionItemKind.Interface },
            { "Struct", CompletionItemKind.Class }, // TODO: Is struct missing from enum?

            // variables
            { "Local", CompletionItemKind.Variable },
            { "Parameter", CompletionItemKind.Variable },
            { "RangeVariable", CompletionItemKind.Variable },

            // members
            { "Const", CompletionItemKind.Value }, // TODO: Is const missing from enum?
            { "EnumMember", CompletionItemKind.Enum },
            { "Event", CompletionItemKind.Function }, // TODO: Is event missing from enum?
            { "Field", CompletionItemKind.Field },
            { "Method", CompletionItemKind.Method },
            { "Property", CompletionItemKind.Property },

            // other stuff
            { "Label", CompletionItemKind.Unit }, // need a better option for this.
            { "Keyword", CompletionItemKind.Keyword },
            { "Namespace", CompletionItemKind.Module }
        };

        private static CompletionItemKind GetCompletionItemKind(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return CompletionItemKind.Property;
            }
            if(_kind.TryGetValue(key, out var completionItemKind))
            {
                return completionItemKind;
            }
            return CompletionItemKind.Property;
        }

        public CompletionHandler(Mef.IRequestHandler<AutoCompleteRequest, IEnumerable<AutoCompleteResponse>> autoCompleteHandler, DocumentSelector documentSelector)
        {
            _autoCompleteHandler = autoCompleteHandler;
            _documentSelector = documentSelector;
        }

        public async Task<CompletionList> Handle(CompletionParams request, CancellationToken token)
        {
            var omnisharpRequest = new AutoCompleteRequest()
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                Column = Convert.ToInt32(request.Position.Character),
                Line = Convert.ToInt32(request.Position.Line),
                WantKind = true,
                WantDocumentationForEveryCompletionResult = true,
                WantReturnType = true
            };

            var omnisharpResponse = await _autoCompleteHandler.Handle(omnisharpRequest);

            var completions = new Dictionary<string, List<CompletionItem>>();
            foreach (var response in omnisharpResponse)
            {
                var completionItem = new CompletionItem {
                    Label = response.CompletionText,
                    Detail = !string.IsNullOrEmpty(response.ReturnType) ?
                            response.DisplayText :
                            $"{response.ReturnType} {response.DisplayText}",
                    Documentation = response.Description,
                    Kind = GetCompletionItemKind(response.Kind),
                    InsertText = response.CompletionText,
                };

                if(!completions.ContainsKey(completionItem.Label))
                {
                    completions[completionItem.Label] = new List<CompletionItem>();
                }
                completions[completionItem.Label].Add(completionItem);
            }

            var result = new List<CompletionItem>();
            foreach (var key in completions.Keys)
            {
                var suggestion = completions[key][0];
                var overloadCount = completions[key].Count - 1;

                if (overloadCount > 0)
                {
                    // indicate that there is more
                    suggestion.Detail = $"{suggestion.Detail} (+ {overloadCount} overload(s))";
                }

                result.Add(suggestion);
            }

            return new CompletionList(result);
        }

        public CompletionRegistrationOptions GetRegistrationOptions()
        {
            return new CompletionRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                TriggerCharacters = new[] { "." },
            };
        }

        public void SetCapability(CompletionCapability capability)
        {
            _capability = capability;
        }
    }
}
