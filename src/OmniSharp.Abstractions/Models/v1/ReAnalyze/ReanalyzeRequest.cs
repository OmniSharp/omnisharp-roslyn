using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Abstractions.Models.V1.ReAnalyze
{
    [OmniSharpEndpoint(OmniSharpEndpoints.ReAnalyze, typeof(ReAnalyzeRequest), typeof(ReanalyzeResponse))]
    public class ReAnalyzeRequest: IRequest
    {
        // This document path is used as context to resolve which project should be analyzed. Simplifies
        // clients since they don't have to figure out what is correct project for current open file.
        public string CurrentOpenFilePathAsContext { get; set; }
    }
}