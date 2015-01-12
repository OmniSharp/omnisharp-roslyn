using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("currentfilemembersastree")]
        public async Task<IActionResult> MembersAsTree([FromBody]Request request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return new HttpNotFoundResult();
            }
            return new ObjectResult(new
            {
                TopLevelTypeDefinitions = StructureComputer.Compute((CSharpSyntaxNode)await document.GetSyntaxRootAsync())
            });
        }

        [HttpPost("currentfilemembersasflat")]
        public async Task<IActionResult> MembersAsFlat([FromBody]Request request)
        {
            var document = _workspace.GetDocument(request.FileName);
            if (document == null)
            {
                return new HttpNotFoundResult();
            }
            var stack = new List<Node>(StructureComputer.Compute((CSharpSyntaxNode)await document.GetSyntaxRootAsync()));
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