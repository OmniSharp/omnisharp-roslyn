using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.V2.CodeActions;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Models;
using Diagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpCodeActionHandler : CodeActionHandlerBase, IExecuteCommandHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(
            RequestHandlers handlers,
            ISerializer serializer,
            ILanguageServer mediator,
            DocumentVersions versions)
        {
            foreach (var (selector, getActionsHandler, runActionHandler) in handlers
                     .OfType<Mef.IRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse>,
                             Mef.IRequestHandler<RunCodeActionRequest, RunCodeActionResponse>>())
            {
                yield return new OmniSharpCodeActionHandler(getActionsHandler, runActionHandler, selector, serializer, mediator, versions);
            }
        }

        private readonly Mef.IRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse> _getActionsHandler;
        private readonly ExecuteCommandRegistrationOptions _executeCommandRegistrationOptions;
        private ExecuteCommandCapability _executeCommandCapability;
        private Mef.IRequestHandler<RunCodeActionRequest, RunCodeActionResponse> _runActionHandler;
        private readonly DocumentSelector _documentSelector;
        private readonly ISerializer _serializer;
        private readonly ILanguageServer _server;
        private readonly DocumentVersions _documentVersions;

        public OmniSharpCodeActionHandler(
            Mef.IRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse> getActionsHandler,
            Mef.IRequestHandler<RunCodeActionRequest, RunCodeActionResponse> runActionHandler,
            DocumentSelector documentSelector,
            ISerializer serializer,
            ILanguageServer server,
            DocumentVersions documentVersions)
        {
            _getActionsHandler = getActionsHandler;
            _runActionHandler = runActionHandler;
            _documentSelector = documentSelector;
            _serializer = serializer;
            _server = server;
            _documentVersions = documentVersions;
            _executeCommandRegistrationOptions = new ExecuteCommandRegistrationOptions()
            {
                Commands = new Container<string>("omnisharp/executeCodeAction"),
            };
        }

        public override async Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken)
        {
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
                if (ca.Identifier.StartsWith("using ")) { kind = CodeActionKind.SourceOrganizeImports; }
                else if (ca.Identifier.StartsWith("Inline ")) { kind = CodeActionKind.RefactorInline; }
                else if (ca.Identifier.StartsWith("Extract ")) { kind = CodeActionKind.RefactorExtract; }
                else if (ca.Identifier.StartsWith("Change ")) { kind = CodeActionKind.QuickFix; }
                else { kind = CodeActionKind.Refactor; }

                codeActions.Add(
                    new CodeAction
                    {
                        Title = ca.Name,
                        Kind = kind,
                        Diagnostics = new Container<Diagnostic>(),
                        Edit = new WorkspaceEdit(),
                        Command = Command.Create("omnisharp/executeCodeAction")
                            .WithArguments(new CommandData()
                            {
                                Uri = request.TextDocument.Uri,
                                Identifier = ca.Identifier,
                                Name = ca.Name,
                                Range = request.Range,
                            })
                            with { Title = ca.Name }
                    });
            }

            return new CommandOrCodeActionContainer(
                codeActions.Select(ca => new CommandOrCodeAction(ca)));
        }

        public override Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request);
        }

        public async Task<Unit> Handle(ExecuteCommandParams request, CancellationToken cancellationToken)
        {
            Debug.Assert(request.Command == "omnisharp/executeCodeAction");
            var data = request.ExtractArguments<CommandData>(_serializer);

            var omnisharpCaRequest = new RunCodeActionRequest {
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
                ;

                await _server.Workspace.ApplyWorkspaceEdit(new ApplyWorkspaceEditParams()
                {
                    Label = data.Name,
                    Edit = edit
                }, cancellationToken);

                // Do something with response?
                //if (response.Applied)
            }

            return Unit.Value;
        }

        class CommandData
        {
            public DocumentUri Uri { get; set;}
            public string Identifier { get; set;}
            public string Name { get; set;}
            public Range Range { get; set;}
        }

        ExecuteCommandRegistrationOptions IRegistration<ExecuteCommandRegistrationOptions, ExecuteCommandCapability>.GetRegistrationOptions(ExecuteCommandCapability capability, ClientCapabilities clientCapabilities)
        {
            _executeCommandCapability = capability;
            return _executeCommandRegistrationOptions;
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
            };
        }
    }
}
