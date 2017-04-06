using System;

namespace OmniSharp.DotNetTest.Models
{
    public class DebugDotNetTestReadyResponse : IAggregateResponse
    {
        public IAggregateResponse Merge(IAggregateResponse response)
        {
            return response;
        }
    }
}
