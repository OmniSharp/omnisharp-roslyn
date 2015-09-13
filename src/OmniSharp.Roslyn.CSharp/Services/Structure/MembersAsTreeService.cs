using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using OmniSharp.Models;

namespace OmniSharp
{
    [Export(typeof(RequestHandler<Request, FileMemberTree>))]
    public class MembersAsTreeService : RequestHandler<Request, FileMemberTree>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public MembersAsTreeService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public async Task<FileMemberTree> Handle(Request request)
        {
            return new FileMemberTree()
            {
                TopLevelTypeDefinitions = await StructureComputer.Compute(_workspace.GetDocuments(request.FileName))
            };
        }
    }
}
