namespace OmniSharp.Models.FileClose
{
    public class FileCloseResponse : IAggregateResponse
    {
        public IAggregateResponse Merge(IAggregateResponse response) { return response; }
    }
}
