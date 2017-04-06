using System;

namespace OmniSharp.DotNetTest.Models
{
    public class DebugTestReadyResponse : IAggregateResponse
    {
        public bool IsReady { get; set; }

        public IAggregateResponse Merge(IAggregateResponse response)
        {
            // Need this to keep endpoint middleware happy.
            return response ?? new DebugTestReadyResponse();
        }
    }
}
