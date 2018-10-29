using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Abstractions.Services;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models.MembersTree;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Structure
{
    [OmniSharpHandler(OmniSharpEndpoints.MembersTree, LanguageNames.CSharp)]
    public class MembersAsTreeService : IRequestHandler<MembersTreeRequest, FileMemberTree>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly IEnumerable<ISyntaxFeaturesDiscover> _discovers;
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        [ImportingConstructor]
        public MembersAsTreeService(OmniSharpWorkspace workspace, [ImportMany] IEnumerable<ISyntaxFeaturesDiscover> featureDiscovers, [ImportMany] IEnumerable<IProjectSystem> projectSystems)
        {
            _workspace = workspace;
            _discovers = featureDiscovers;
            _projectSystems = projectSystems;
        }

        public async Task<FileMemberTree> Handle(MembersTreeRequest request)
        {
            // Waiting until the document is fully formed in memory (for project systems that have this ability) 
            // helps to reduce chances of returning invalid/incomplete member structure for the document while compilation is still in progress.
            await _projectSystems.WaitForAllProjectsToLoadForFileAsync(request.FileName);

            return new FileMemberTree()
            {
                TopLevelTypeDefinitions = await StructureComputer.Compute(_workspace.GetDocuments(request.FileName), _discovers)
            };
        }
    }
}
