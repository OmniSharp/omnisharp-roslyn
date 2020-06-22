using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FindSymbols;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    internal sealed class OmniSharpWorkspaceSymbolsHandler : WorkspaceSymbolsHandler
    {
        private readonly IRequestHandler<FindSymbolsRequest, QuickFixResponse> _FindSymbolsHandler;

        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            foreach (var (selector, findSymbolsHandler) in handlers
                     .OfType<Mef.IRequestHandler<FindSymbolsRequest, QuickFixResponse>>())
            {
                //
                // TODO: remove the conditional when we upgrade to v0.16.0 of Omnisharp.Extensions.LSP
                //
                // we have no WorkspaceSymbolRegistrationOptions/DocumentSelector until version 0.16.0 of Omnisharp.Extensions.LSP
                // thus we artificially limit the handler to C# language for now
                // (as Cake version of <FindSymbolsRequest,QuickFixResponse> would get selected otherwise)
                //
                if (selector.Any(f => f.Pattern == "**/*.cs"))
                {
                    yield return new OmniSharpWorkspaceSymbolsHandler(findSymbolsHandler, selector);
                }
            }
        }

        public OmniSharpWorkspaceSymbolsHandler(
            IRequestHandler<FindSymbolsRequest, QuickFixResponse> findSymbolsHandler,
            DocumentSelector selector)
        {
            _FindSymbolsHandler = findSymbolsHandler;
        }

        public async override Task<SymbolInformationContainer>
        Handle(WorkspaceSymbolParams request, CancellationToken cancellationToken)
        {
            var omnisharpRequest = new FindSymbolsRequest {
                Filter = request.Query,
                MaxItemsToReturn = 100,
            };

            var omnisharpResponse = await _FindSymbolsHandler.Handle(omnisharpRequest);

            var symbols = omnisharpResponse.QuickFixes?.Cast<SymbolLocation>().Select(
                    x => new SymbolInformation {
                        Name = x.Text,
                        Kind = Helpers.ToSymbolKind(x.Kind),
                        Location = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Location {
                            Uri = Helpers.ToUri(x.FileName),
                            Range = Helpers.ToRange((x.Column, x.Line), (x.EndColumn, x.EndLine))
                        }
                    })
                .ToArray();

            return symbols ?? Array.Empty<SymbolInformation>();
        }
    }
}
