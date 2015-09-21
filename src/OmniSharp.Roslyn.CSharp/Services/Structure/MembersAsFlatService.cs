using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Structure
{
    [OmniSharpHandler(typeof(RequestHandler<MembersFlatRequest, IEnumerable<QuickFix>>), LanguageNames.CSharp)]
    public class MembersAsFlatService : RequestHandler<MembersFlatRequest, IEnumerable<QuickFix>>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public MembersAsFlatService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<IEnumerable<QuickFix>> Handle(MembersFlatRequest request)
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
            return ret;
        }
    }
}
