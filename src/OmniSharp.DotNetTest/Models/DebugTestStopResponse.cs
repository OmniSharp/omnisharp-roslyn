using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    public class DebugTestStopResponse : IAggregateResponse
    {
        public IAggregateResponse Merge(IAggregateResponse response)
        {
            // Need this to keep endpoint middleware happy.
            return response ?? new DebugTestStopResponse();
        }
    }
}
