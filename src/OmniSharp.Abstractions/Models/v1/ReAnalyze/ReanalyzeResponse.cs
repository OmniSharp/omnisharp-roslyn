using OmniSharp.Models;

namespace OmniSharp.Abstractions.Models.V1.ReAnalyze
{
    public class ReanalyzeResponse : IAggregateResponse
    {
        public IAggregateResponse Merge(IAggregateResponse response) { return response; }
    }
}