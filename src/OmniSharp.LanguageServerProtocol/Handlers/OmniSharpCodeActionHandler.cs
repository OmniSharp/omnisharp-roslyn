using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using Diagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;
using OmniSharp.Models.V2.CodeActions;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpCodeActionHandler : CodeActionHandlerBase
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(
            RequestHandlers handlers,
            ILanguageServer mediator,
            DocumentVersions versions)
        {
            foreach (var (selector, getActionsHandler, runActionHandler) in handlers
                     .OfType<Mef.IRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse>,
                             Mef.IRequestHandler<RunCodeActionRequest, RunCodeActionResponse>>())
            {
                yield return new OmniSharpCodeActionHandler(getActionsHandler, runActionHandler, selector, mediator, versions);
            }
        }

        private readonly Mef.IRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse> _getActionsHandler;
        private readonly Mef.IRequestHandler<RunCodeActionRequest, RunCodeActionResponse> _runActionHandler;
        private readonly DocumentSelector _documentSelector;
        private readonly ILanguageServer _server;
        private readonly DocumentVersions _documentVersions;

        public OmniSharpCodeActionHandler(
            Mef.IRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse> getActionsHandler,
            Mef.IRequestHandler<RunCodeActionRequest, RunCodeActionResponse> runActionHandler,
            DocumentSelector documentSelector,
            ILanguageServer server,
            DocumentVersions documentVersions)
        {
            _getActionsHandler = getActionsHandler;
            _runActionHandler = runActionHandler;
            _documentSelector = documentSelector;
            _server = server;
            _documentVersions = documentVersions;
        }

        public override async Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken)
        {
            var codeActionCaps = _server.ClientSettings.Capabilities.TextDocument.CodeAction.Value;
            bool clientCanResolveEditProp = codeActionCaps?.ResolveSupport?.Properties.Contains("edit") ?? false;

            var omnisharpRequest = new GetCodeActionsRequest
            {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                Column = request.Range.Start.Character,
                Line = request.Range.Start.Line,
                Selection = Helpers.FromRange(request.Range),
            };

            var omnisharpResponse = await _getActionsHandler.Handle(omnisharpRequest);

            var codeActions = new List<CodeAction>();

            foreach (var ca in omnisharpResponse.CodeActions)
            {
                CodeActionKind kind;
                if (ca.Identifier.StartsWith("using ")) { kind = CodeActionKind.QuickFix; }
                else if (ca.Identifier.StartsWith("Inline ")) { kind = CodeActionKind.RefactorInline; }
                else if (ca.Identifier.StartsWith("Extract ")) { kind = CodeActionKind.RefactorExtract; }
                else if (ca.Identifier.StartsWith("Change ")) { kind = CodeActionKind.QuickFix; }
                else { kind = CodeActionKind.Refactor; }

                var codeAction = new CodeAction {
                    Title = ca.Name,
                    Kind = kind,
                    Diagnostics = new Container<Diagnostic>(),
                    Edit = null,
                    Data = JObject.FromObject(
                        new CommandData()
                        {
                            Uri = request.TextDocument.Uri,
                            Identifier = ca.Identifier,
                            Name = ca.Name,
                            Range = request.Range,
                        })
                };

                if (!clientCanResolveEditProp)
                {
                    var codeActionResolution = await this.Handle(codeAction, cancellationToken);

                    codeAction = codeAction with {
                        Edit = codeActionResolution.Edit,
                        Data = null,
                    };
                }

                codeActions.Add(codeAction);
            }

            return new CommandOrCodeActionContainer(
                codeActions.Select(ca => new CommandOrCodeAction(ca)));
        }

        public override async Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken)
        {
            var data = request.Data.ToObject<CommandData>();

            var omnisharpCaRequest = new RunCodeActionRequest
            {
                Identifier = data.Identifier,
                FileName = data.Uri.GetFileSystemPath(),
                Column = data.Range.Start.Character,
                Line = data.Range.Start.Line,
                Selection = Helpers.FromRange(data.Range),
                ApplyTextChanges = false,
                WantsTextChanges = true,
                WantsAllCodeActionOperations = true
            };

            var omnisharpCaResponse = await _runActionHandler.Handle(omnisharpCaRequest);
            if (omnisharpCaResponse.Changes != null)
            {
                var edit = Helpers.ToWorkspaceEdit(
                    omnisharpCaResponse.Changes,
                    _server.ClientSettings.Capabilities.Workspace!.WorkspaceEdit.Value,
                    _documentVersions
                );

                return new CodeAction
                {
                    Edit = edit,
                };
            }
            else
            {
                return new CodeAction();
            }
        }

        class CommandData
        {
            public DocumentUri Uri { get; set;}
            public string Identifier { get; set;}
            public string Name { get; set;}
            public Range Range { get; set;}
        }

        protected override CodeActionRegistrationOptions CreateRegistrationOptions(CodeActionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CodeActionRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                CodeActionKinds = new Container<CodeActionKind>(
                    CodeActionKind.SourceOrganizeImports,
                    CodeActionKind.Refactor,
                    CodeActionKind.RefactorExtract),
                ResolveProvider = true,
            };
        }
    }
}
