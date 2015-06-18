using System.Linq;
using Microsoft.AspNet.Mvc;
using OmniSharp.Models;

namespace OmniSharp.Filters
{
    public class UpdateBufferFilter : IActionFilter
    {
        private OmnisharpWorkspace _workspace;

        public UpdateBufferFilter(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionArguments.Any())
            {
                var request = context.ActionArguments.FirstOrDefault(arg => arg.Value is Request);
                if (request.Value != null)
                {
                    _workspace.BufferManager.UpdateBuffer((Request)request.Value);
                }
            }
        }
    }
}
