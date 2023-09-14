using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FindSymbols;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpWorkspaceSymbolsHandler : WorkspaceSymbolsHandlerBase
    {
        private readonly IEnumerable<IRequestHandler<FindSymbolsRequest, QuickFixResponse>> _findSymbolsHandlers;

        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            //
            // TODO: remove the conditional when we upgrade to v0.16.0 of Omnisharp.Extensions.LSP
            //
            // we have no WorkspaceSymbolRegistrationOptions/DocumentSelector until version 0.16.0 of Omnisharp.Extensions.LSP
            // thus we artificially limit the handler to C# language for now
            // (as Cake version of <FindSymbolsRequest,QuickFixResponse> would get selected otherwise)
            //
            yield return new OmniSharpWorkspaceSymbolsHandler(handlers
                .OfType<Mef.IRequestHandler<FindSymbolsRequest, QuickFixResponse>>()
                .Select(z => z.handler));
        }

        public OmniSharpWorkspaceSymbolsHandler(
            IEnumerable<IRequestHandler<FindSymbolsRequest, QuickFixResponse>> findSymbolsHandlers)
        {
            _findSymbolsHandlers = findSymbolsHandlers.ToArray();
        }

        public override async Task<Container<WorkspaceSymbol>> Handle(
            WorkspaceSymbolParams request,
            CancellationToken cancellationToken)
        {
            var omnisharpRequest = new FindSymbolsRequest
            {
                Filter = request.Query,
                MaxItemsToReturn = 100,
            };

            var responses = await Task.WhenAll(
                _findSymbolsHandlers.Select(handler => handler.Handle(omnisharpRequest))
            );

            return responses
                .SelectMany(z => z?.QuickFixes.OfType<SymbolLocation>() ?? Enumerable.Empty<SymbolLocation>())
                .Select(
                    x => new WorkspaceSymbol
                    {
                        Name = x.Text,
                        Kind = Helpers.ToSymbolKind(x.Kind),
                        ContainerName = x.ContainingSymbolName,
                        Location = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Location
                        {
                            Uri = Helpers.ToUri(x.FileName),
                            Range = Helpers.ToRange((x.Column, x.Line), (x.EndColumn, x.EndLine))
                        }
                    })
                .ToArray();
        }

        protected override WorkspaceSymbolRegistrationOptions CreateRegistrationOptions(WorkspaceSymbolCapability capability, ClientCapabilities clientCapabilities)
        {
            return new WorkspaceSymbolRegistrationOptions() { };
        }
    }
}
