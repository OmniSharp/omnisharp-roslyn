using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(typeof(RequestHandler<GotoFileRequest, QuickFixResponse>), LanguageNames.CSharp)]
    public class GotoFileService : RequestHandler<GotoFileRequest, QuickFixResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public GotoFileService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public Task<QuickFixResponse> Handle(GotoFileRequest request)
        {
            var docs = _workspace.CurrentSolution.Projects.SelectMany(project => project.Documents).
                GroupBy(x => x.FilePath). //group in case same file is added to multiple projects
                Select(x => new QuickFix { FileName = x.Key, Text = x.First().Name, Line = 1, Column = 1 });

            return Task.FromResult(new QuickFixResponse(docs));
        }
    }
}
