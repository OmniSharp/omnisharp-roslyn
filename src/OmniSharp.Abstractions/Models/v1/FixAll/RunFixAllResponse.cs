using System.Collections.Generic;
using OmniSharp.Models;

namespace OmniSharp.Abstractions.Models.V1.FixAll
{
    public class RunFixAllResponse : IAggregateResponse
    {
        public RunFixAllResponse()
        {
            Changes = new List<ModifiedFileResponse>();
        }

        public IEnumerable<ModifiedFileResponse> Changes { get; set; }

        public IAggregateResponse Merge(IAggregateResponse response) { return response; }
    }
}
