using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Services;

namespace OmniSharp.Roslyn.CSharp.Services.Buffer
{
    [OmniSharpHandler(OmniSharpEndpoints.UpdateBuffer, LanguageNames.CSharp)]
    public class UpdateBufferService : IRequestHandler<UpdateBufferRequest, object>
    {
        private OmniSharpWorkspace _workspace;
        private readonly IEnumerable<IProjectSystem> _projectSystems;

        [ImportingConstructor]
        public UpdateBufferService(OmniSharpWorkspace workspace, [ImportMany] IEnumerable<IProjectSystem> projectSystems)
        {
            _workspace = workspace;
            _projectSystems = projectSystems;
        }

        public async Task<object> Handle(UpdateBufferRequest request)
        {
            // Waiting until the document is fully formed in memory (for project systems that have this ability) 
            // before applying updates to it helps to reduce chances for the compiler getting confused and producing invalid compilation errors.
            await _projectSystems.WaitForAllProjectsToLoadForFileAsync(request.FileName);
            await _workspace.BufferManager.UpdateBufferAsync(request);
            return true;
        }
    }
}
