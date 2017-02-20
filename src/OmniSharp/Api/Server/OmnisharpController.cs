using Microsoft.AspNet.Mvc;
using Microsoft.Framework.Runtime;

namespace OmniSharp.Api.Server
{
    public partial class OmnisharpController : Controller
    {
        protected readonly IApplicationShutdown _applicationShutdown;
        public OmnisharpController(IApplicationShutdown applicationShutdown)
        {
            _applicationShutdown = applicationShutdown;
        }

        [HttpPost("/stopserver")]
        public void StopServer()
        {
            _applicationShutdown.RequestShutdown();
        }
    }
}