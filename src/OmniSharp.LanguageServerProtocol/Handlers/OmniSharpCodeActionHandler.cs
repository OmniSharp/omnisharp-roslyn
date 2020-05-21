using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models;
using OmniSharp.Models.V2.CodeActions;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpCodeActionHandler: CodeActionHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, getActionsHandler, runActionHandler) in handlers
                     .OfType<Mef.IRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse>,
                             Mef.IRequestHandler<RunCodeActionRequest, RunCodeActionResponse>>())
            {
                yield return new OmniSharpCodeActionHandler(getActionsHandler, runActionHandler, selector);
            }
        }

        private readonly Mef.IRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse> _getActionsHandler;
        private readonly Mef.IRequestHandler<RunCodeActionRequest, RunCodeActionResponse> _runActionHandler;

        public OmniSharpCodeActionHandler(
            Mef.IRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse> getActionsHandler,
            Mef.IRequestHandler<RunCodeActionRequest, RunCodeActionResponse> runActionHandler,
            DocumentSelector documentSelector)
            : base(new CodeActionRegistrationOptions()
            {
                DocumentSelector = documentSelector,
                CodeActionKinds = new Container<CodeActionKind>(
                    CodeActionKind.SourceOrganizeImports,
                    CodeActionKind.Refactor,
                    CodeActionKind.RefactorExtract),
            })
        {
            _getActionsHandler = getActionsHandler;
            _runActionHandler = runActionHandler;
        }

        public async override Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken)
        {
            var omnisharpRequest = new GetCodeActionsRequest {
                FileName = Helpers.FromUri(request.TextDocument.Uri),
                Column = (int)request.Range.Start.Character,
                Line = (int)request.Range.Start.Line,
                Selection = Helpers.FromRange(request.Range),
            };

            var omnisharpResponse = await _getActionsHandler.Handle(omnisharpRequest);

            var codeActions = new List<CodeAction>();

            foreach (var ca in omnisharpResponse.CodeActions)
            {
                var omnisharpCaRequest = new RunCodeActionRequest {
                    Identifier = ca.Identifier,
                    FileName = Helpers.FromUri(request.TextDocument.Uri),
                    Column = Convert.ToInt32(request.Range.Start.Character),
                    Line = Convert.ToInt32(request.Range.Start.Line),
                    Selection = Helpers.FromRange(request.Range),
                    ApplyTextChanges = false,
                    WantsTextChanges = true,
                };

                var omnisharpCaResponse = await _runActionHandler.Handle(omnisharpCaRequest);

                var changes = omnisharpCaResponse.Changes.ToDictionary(
                    x => Helpers.ToUri(x.FileName),
                    x => ((ModifiedFileResponse)x).Changes.Select(edit => new TextEdit
                    {
                        NewText = edit.NewText,
                        Range = Helpers.ToRange((edit.StartColumn, edit.StartLine), (edit.EndColumn, edit.EndLine))
                    }));

                CodeActionKind kind;
                if (ca.Identifier.StartsWith("using ")) { kind = CodeActionKind.SourceOrganizeImports; }
                else if (ca.Identifier.StartsWith("Inline ")) { kind = CodeActionKind.RefactorInline; }
                else if (ca.Identifier.StartsWith("Extract ")) { kind = CodeActionKind.RefactorExtract; }
                else if (ca.Identifier.StartsWith("Change ")) { kind = CodeActionKind.QuickFix; }
                else { kind = CodeActionKind.Refactor; }

                codeActions.Add(
                    new CodeAction {
                        Title = ca.Name,
                        Kind = kind,
                        Diagnostics = new Container<Diagnostic>(),
                        Edit = new WorkspaceEdit { Changes = changes, }
                    });
            }

            return new CommandOrCodeActionContainer(
                codeActions.Select(ca => new CommandOrCodeAction(ca)));
        }
    }
}
