using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Structure
{
    [OmniSharpHandler(typeof(RequestHandler<MembersTreeRequest, FileMemberTree>), LanguageNames.CSharp)]
    public class MembersAsTreeService : RequestHandler<MembersTreeRequest, FileMemberTree>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public MembersAsTreeService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<FileMemberTree> Handle(MembersTreeRequest request)
        {
            return new FileMemberTree()
            {
                TopLevelTypeDefinitions = await StructureComputer.Compute(_workspace.GetDocuments(request.FileName))
            };
        }
    }
}
