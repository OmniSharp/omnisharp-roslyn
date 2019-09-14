using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.Abstractions.Models.V1.ReAnalyze
{
    [OmniSharpEndpoint(OmniSharpEndpoints.ReAnalyze, typeof(ReAnalyzeRequest), typeof(ReanalyzeResponse))]
    public class ReAnalyzeRequest: SimpleFileRequest
    {
    }
}