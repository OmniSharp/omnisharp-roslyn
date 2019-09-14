using System;
using System.Threading.Tasks;
using OmniSharp.Protocol;

namespace OmniSharp.Endpoint
{
    public class GenericEndpointHandler : EndpointHandler
    {
        private readonly Func<RequestPacket, Task<object>> _action;

        public GenericEndpointHandler(Func<RequestPacket, Task<object>> action)
        {
            _action = action;
        }

        public override Task<object> Handle(RequestPacket context)
        {
            return _action(context);
        }
    }
}
