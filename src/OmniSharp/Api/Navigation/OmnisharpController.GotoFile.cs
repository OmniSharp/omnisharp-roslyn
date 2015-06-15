using System.Linq;
using Microsoft.AspNet.Mvc;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("gotofile")]
        public QuickFixResponse GoToFile(Request request)
        {
            var docs = _workspace.CurrentSolution.Projects.SelectMany(project => project.Documents).
                GroupBy(x => x.FilePath). //group in case same file is added to multiple projects
                Select(x => new QuickFix { FileName = x.Key, Text = x.First().Name, Line = 1, Column = 1});
                
            return new QuickFixResponse(docs);
        }
    }
}