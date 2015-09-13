using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [Export(typeof(RequestHandler<Request, QuickFixResponse>))]
    public class GotoFileService : RequestHandler<Request, QuickFixResponse>
    {
        private readonly OmnisharpWorkspace _workspace;

        [ImportingConstructor]
        public GotoFileService(OmnisharpWorkspace workspace)
        {
            _workspace = workspace;
        }

        public Task<QuickFixResponse> Handle(Request request)
        {
            var docs = _workspace.CurrentSolution.Projects.SelectMany(project => project.Documents).
                GroupBy(x => x.FilePath). //group in case same file is added to multiple projects
                Select(x => new QuickFix { FileName = x.Key, Text = x.First().Name, Line = 1, Column = 1 });

            return Task.FromResult(new QuickFixResponse(docs));
        }
    }
}
