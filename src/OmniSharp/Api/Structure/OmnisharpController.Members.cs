using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("currentfilemembersastree")]
        public async Task<IActionResult> MembersAsTree(Request request)
        {
            return new ObjectResult(new
            {
                TopLevelTypeDefinitions = await StructureComputer.Compute(_workspace.GetDocuments(request.FileName))
            });
        }

        [HttpPost("currentfilemembersasflat")]
        public async Task<IActionResult> MembersAsFlat(Request request)
        {
            var stack = new List<FileMemberElement>(await StructureComputer.Compute(_workspace.GetDocuments(request.FileName)));
            var ret = new List<QuickFix>();
            while (stack.Count > 0)
            {
                var node = stack[0];
                stack.Remove(node);
                ret.Add(node.Location);
                stack.AddRange(node.ChildNodes);
            }
            return new ObjectResult(ret);
        }
    }
}