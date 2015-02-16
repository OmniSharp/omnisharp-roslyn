using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.DependencyInjection;
using OmniSharp.Stdio.Protocol;

namespace OmniSharp.Stdio.Transport
{
    public class RequestHandler
    {
        private readonly IServiceProvider provider;
        private readonly TextReader input;
        private readonly TextWriter output;

        public RequestHandler(IServiceProvider provider, TextReader input, TextWriter output)
        {
            this.provider = provider;
            this.input = input;
            this.output = output;
        }

        public void Start(CancellationToken token)
        {
            output.WriteLine(new EventPacket() {
                Event = "started"
            });

            while (!token.IsCancellationRequested)
            {
                var line = input.ReadLine();
                HandleLine(line);
            }
        }

        private void HandleLine(string line)
        {
            Task.Factory.StartNew(async () =>
            {
                RequestPacket request;
                try
                {
                    request = new RequestPacket(line);
                }
                catch (Exception e)
                {
                    output.WriteLine(new EventPacket()
                    {
                        Event = "error",
                        Body = e.ToString()
                    });

                    return;
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
                    output.WriteLine(response);
                }
                
                // last but not least send the response
                output.WriteLine(response);
            });
        }

        private async Task<object> PerformRequest(MethodInfo target, RequestPacket request)
        {
            var receiver = provider.GetRequiredService(target.DeclaringType);
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
