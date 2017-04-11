using System;

namespace OmniSharp.DotNetTest.Models
{
    public class DebugTestRunResponse : IAggregateResponse
    {
        public IAggregateResponse Merge(IAggregateResponse response)
        {
            // Need this to keep endpoint middleware happy.
            return response ?? new DebugTestRunResponse();
        }
    }
}
