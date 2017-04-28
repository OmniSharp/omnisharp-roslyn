namespace OmniSharp.Models.FileOpen
{
    public class FileOpenResponse : IAggregateResponse
    {
        public IAggregateResponse Merge(IAggregateResponse response) { return response; }
    }
}
