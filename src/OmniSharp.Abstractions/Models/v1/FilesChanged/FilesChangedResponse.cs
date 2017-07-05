namespace OmniSharp.Models.FilesChanged
{
    public class FilesChangedResponse : IAggregateResponse
    {
        public IAggregateResponse Merge(IAggregateResponse response)
        {
            // File Changes just need to inform plugins, editors are not expecting any kind of real response
            return response ?? new FilesChangedResponse();
        }
    }
}
