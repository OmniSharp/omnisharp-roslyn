using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Models;
using OmniSharp.Models.V2.CodeActions;
using LSP = OmniSharp.Extensions.LanguageServer.Protocol;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class OmniSharpExecuteCommandHandler : ExecuteCommandHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(
            RequestHandlers handlers,
            ILanguageServerWorkspace mediator)
        {
            foreach (var (selector, runActionHandler) in handlers
                     .OfType<Mef.IRequestHandler<RunCodeActionRequest, RunCodeActionResponse>>())
            {
                if (runActionHandler != null)
                    yield return new OmniSharpExecuteCommandHandler(runActionHandler, mediator, selector);
            }
        }

        private readonly Mef.IRequestHandler<RunCodeActionRequest, RunCodeActionResponse> _runActionHandler;
        private readonly ILanguageServerWorkspace _mediator;

        public OmniSharpExecuteCommandHandler(
            Mef.IRequestHandler<RunCodeActionRequest, RunCodeActionResponse> runActionHandler,
            ILanguageServerWorkspace mediator,
            DocumentSelector selector)
            : base (new ExecuteCommandRegistrationOptions() {
                Commands = new Container<string>(
                    "omnisharp/executeCodeAction"),
            })
        {
            _runActionHandler = runActionHandler;
            _mediator = mediator;
        }

        public override async Task<Unit>
        Handle(ExecuteCommandParams request, CancellationToken cancellationToken)
        {
            if (request.Command == "omnisharp/executeCodeAction")
            {
                var textDocumentUri = request.Arguments[0].ToObject<Uri>();
                var caIdentifier = request.Arguments[1].ToObject<string>();
                var caName = request.Arguments[2].ToObject<string>();
                var range = request.Arguments[3].ToObject<LSP.Models.Range>();

                var omnisharpCaRequest = new RunCodeActionRequest {
                    Identifier = caIdentifier,
                    FileName = Helpers.FromUri(textDocumentUri),
                    Column = Convert.ToInt32(range.Start.Character),
                    Line = Convert.ToInt32(range.Start.Line),
                    Selection = Helpers.FromRange(range),
                    ApplyTextChanges = false,
                    WantsTextChanges = true,
                };

                var omnisharpCaResponse = await _runActionHandler.Handle(omnisharpCaRequest);

                var changes = omnisharpCaResponse.Changes.ToDictionary(
                    x => Helpers.ToUri(x.FileName),
                    x => ((ModifiedFileResponse)x).Changes.Select(
                        edit => new TextEdit
                        {
                            NewText = edit.NewText,
                            Range = Helpers.ToRange((edit.StartColumn, edit.StartLine), (edit.EndColumn, edit.EndLine))
                        }));

                await _mediator.ApplyEdit(
                    new ApplyWorkspaceEditParams {
                        Label = caName,
                        Edit = new WorkspaceEdit { Changes = changes },
                    });
            }

            return Unit.Value;
        }
    }
}
