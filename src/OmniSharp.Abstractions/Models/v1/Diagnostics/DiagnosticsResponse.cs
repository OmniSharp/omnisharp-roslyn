namespace OmniSharp.Models.Diagnostics
{
    public class DiagnosticsResponse : IAggregateResponse
    {
        public IAggregateResponse Merge(IAggregateResponse response) { return response; }
    }
}
