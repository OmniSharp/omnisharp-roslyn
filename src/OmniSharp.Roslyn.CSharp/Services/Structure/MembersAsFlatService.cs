using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.MembersFlat;
using OmniSharp.Models.MembersTree;

namespace OmniSharp.Roslyn.CSharp.Services.Structure
{
    [OmniSharpHandler(OmniSharpEndpoints.MembersFlat, LanguageNames.CSharp)]
    public class MembersAsFlatService : IRequestHandler<MembersFlatRequest, IEnumerable<QuickFix>>
    {
        private readonly OmniSharpWorkspace _workspace;

        [ImportingConstructor]
        public MembersAsFlatService(OmniSharpWorkspace workspace)
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
