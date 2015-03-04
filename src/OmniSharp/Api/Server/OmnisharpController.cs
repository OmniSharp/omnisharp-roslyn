using Microsoft.AspNet.Mvc;
using Microsoft.Framework.Runtime;

// For more information on enabling MVC for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

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