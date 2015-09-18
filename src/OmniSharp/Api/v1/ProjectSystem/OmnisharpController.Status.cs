using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.Framework.Runtime;

namespace OmniSharp
{
    public class StatusController
    {
        private readonly IApplicationShutdown _applicationShutdown;

        public StatusController(IApplicationShutdown applicationShutdown)
        {
            _applicationShutdown = applicationShutdown;
        }

        [HttpPost("/stopserver")]
        public bool StopServer()
        {
            Task.Run(() => {
                Thread.Sleep(200);
                _applicationShutdown.RequestShutdown();
            });
            return true;
        }
    }
}
