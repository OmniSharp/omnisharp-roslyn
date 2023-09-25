using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.CodeAnalysis;
using NuGet.Protocol;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Models.V2.CodeActions;
using CodeActionKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.CodeActionKind;
using Diagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;
using OmniSharpCodeActionKind = OmniSharp.Models.V2.CodeActions.CodeActionKind;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

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
        private readonly TextDocumentSelector _documentSelector;
        private readonly ILanguageServer _server;
        private readonly DocumentVersions _documentVersions;

        public OmniSharpCodeActionHandler(
            Mef.IRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse> getActionsHandler,
            Mef.IRequestHandler<RunCodeActionRequest, RunCodeActionResponse> runActionHandler,
            TextDocumentSelector documentSelector,
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
                codeActions.Add(
                    new CodeAction
                    {
                        Title = ca.Name,
                        Kind = OmniSharpCodeActionHandler.FromOmniSharpCodeActionKind(ca.CodeActionKind),
                        Diagnostics = new Container<Diagnostic>(),
                        Data = new CommandData()
                        {
                            Uri = request.TextDocument.Uri,
                            Identifier = ca.Identifier,
                            Name = ca.Name,
                            Range = request.Range,
                        }.ToJToken()
                    });
            }

            return new CommandOrCodeActionContainer(
                codeActions.Select(ca => new CommandOrCodeAction(ca)));
        }

        public override async Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken)
        {
            var data = request.Data.FromJToken<CommandData>();

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
            if (omnisharpCaResponse.Changes == null)
            {
                return request with { Edit = new WorkspaceEdit() };
            }

            var edit = Helpers.ToWorkspaceEdit(
                omnisharpCaResponse.Changes,
                _server.ClientSettings.Capabilities.Workspace!.WorkspaceEdit.Value,
                _documentVersions
            );

            return request with { Edit = edit };
        }

        class CommandData
        {
            public DocumentUri Uri { get; set; }
            public string Identifier { get; set; }
            public string Name { get; set; }
            public Range Range { get; set; }
        }

        private static CodeActionKind FromOmniSharpCodeActionKind(string omnisharpCodeAction)
            => omnisharpCodeAction switch
            {
                OmniSharpCodeActionKind.QuickFix => CodeActionKind.QuickFix,
                OmniSharpCodeActionKind.Refactor => CodeActionKind.Refactor,
                OmniSharpCodeActionKind.RefactorInline => CodeActionKind.RefactorInline,
                OmniSharpCodeActionKind.RefactorExtract => CodeActionKind.RefactorExtract,
                _ => throw new InvalidOperationException($"Unexpected code action kind {omnisharpCodeAction}")
            };

        protected override CodeActionRegistrationOptions CreateRegistrationOptions(CodeActionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CodeActionRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                ResolveProvider = true,
                CodeActionKinds = new Container<CodeActionKind>(
                    CodeActionKind.SourceOrganizeImports,
                    CodeActionKind.Refactor,
                    CodeActionKind.RefactorExtract,
                    CodeActionKind.RefactorInline),
            };
        }
    }
}
