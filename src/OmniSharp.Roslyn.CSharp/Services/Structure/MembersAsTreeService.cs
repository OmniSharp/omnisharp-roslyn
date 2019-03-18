using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Abstractions.Services;
using OmniSharp.Mef;
using OmniSharp.Models.MembersTree;

namespace OmniSharp.Roslyn.CSharp.Services.Structure
{
    [OmniSharpHandler(OmniSharpEndpoints.MembersTree, LanguageNames.CSharp)]
    public class MembersAsTreeService : IRequestHandler<MembersTreeRequest, FileMemberTree>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly IEnumerable<ISyntaxFeaturesDiscover> _discovers;

        [ImportingConstructor]
        public MembersAsTreeService(OmniSharpWorkspace workspace, [ImportMany] IEnumerable<ISyntaxFeaturesDiscover> featureDiscovers)
        {
            _workspace = workspace;
            _discovers = featureDiscovers;
        }

        public async Task<FileMemberTree> Handle(MembersTreeRequest request)
        {
            return new FileMemberTree()
            {
                // To provide complete members tree for the document wait until all projects are loaded.
                TopLevelTypeDefinitions = await StructureComputer.Compute(await _workspace.GetDocumentsFromFullProjectModelAsync(request.FileName), _discovers)
            };
        }
    }
}
