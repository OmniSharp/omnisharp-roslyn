using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    class OmniSharpExecuteCommandHandler : ExecuteCommandHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(RequestHandlers handlers)
        {
            yield return new OmniSharpExecuteCommandHandler();
        }

        public OmniSharpExecuteCommandHandler()
            : base(new ExecuteCommandRegistrationOptions()
            {
                Commands = new Container<string>(),
            })
        {
        }

        public override Task<Unit>
        Handle(ExecuteCommandParams request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Unit.Value);
        }
    }
}
