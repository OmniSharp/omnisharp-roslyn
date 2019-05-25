using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Abstractions.Models.V1.ReAnalyze
{
    [OmniSharpEndpoint(OmniSharpEndpoints.ReAnalyze, typeof(ReAnalyzeRequest), typeof(ReanalyzeResponse))]
    public class ReAnalyzeRequest: IRequest
    {
        // This is used as context to resolve which project should be analyzed, simplifies
        // clients since they don't have to figure out what is correct project for current open file.
        // This is information which is available in omnisharp workspace and so it should be
        // used as source of truth.
        public string CurrentOpenFilePathAsContext { get; set; }
    }
}