using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models.V2.CodeActions;
using Newtonsoft.Json.Linq;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpCodeActionHandler: CodeActionHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, getActionsHandler) in handlers
                     .OfType<Mef.IRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse>>())
            {
                yield return new OmniSharpCodeActionHandler(getActionsHandler, selector);
            }
        }

        private readonly Mef.IRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse> _getActionsHandler;

        public OmniSharpCodeActionHandler(
            Mef.IRequestHandler<GetCodeActionsRequest, GetCodeActionsResponse> getActionsHandler,
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
                        Command = new Command
                        {
                            Title = ca.Name,
                            Name = "omnisharp/executeCodeAction",
                            Arguments = new JArray(
                                request.TextDocument.Uri,
                                ca.Identifier,
                                ca.Name,
                                JObject.FromObject(request.Range)),
                        }
                    });
            }

            return new CommandOrCodeActionContainer(
                codeActions.Select(ca => new CommandOrCodeAction(ca)));
        }
    }
}
