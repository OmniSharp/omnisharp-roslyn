using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Framework.DependencyInjection;
using OmniSharp.Stdio.Protocol;

namespace OmniSharp.Stdio.Transport
{
    public class RequestHandler
    {
        private readonly IServiceProvider _provider;

        public RequestHandler(IServiceProvider provider)
        {
            this._provider = provider;
        }

        public async Task<Packet> HandleRequest(string json)
        {
            RequestPacket request;
            try
            {
                request = new RequestPacket(json);
            }
            catch (Exception e)
            {
                return new EventPacket()
                {
                    Event = "error",
                    Body = e.ToString()
                };
            }

            var response = request.Reply();
            var target = Controllers.LookUp(request.Command);

            if (target != null)
            {
                try
                {
                    response.Body = await PerformRequest(target, request);
                }
                catch (Exception e)
                {
                    response.Success = false;
                    response.Message = e.ToString();
                }
            }
            else
            {
                response.Success = false;
                response.Message = "Command not found";
            }

            return response;
        }

        private async Task<object> PerformRequest(MethodInfo target, RequestPacket request)
        {
            var receiver = _provider.GetRequiredService(target.DeclaringType);
            var parameters = target.GetParameters();
            var arguments = new object[parameters.Length];
            if (parameters.Length == 1)
            {
                arguments[0] = request.Arguments(parameters[0].ParameterType);
            }

            var result = target.Invoke(receiver, arguments);
            if (result is Task)
            {
                var task = (Task)result;
                await task;
                return task.GetType().GetProperty("Result").GetValue(task);
            }
            else
            {
                return result;
            }
        }
    }
}
