using System.Collections.Generic;
using System.Linq;

namespace OmniSharp.Models.TestCommand
{
    public class GetTestCommandResponse : IAggregateResponse
    {
        public string Directory { get; set; }
        public string TestCommand { get; set; }

        public IEnumerable<QuickFix> QuickFixes { get; set; }

        public IAggregateResponse Merge(IAggregateResponse response)
        {
            var quickFixResponse = (QuickFixResponse)response;
            return new QuickFixResponse(this.QuickFixes.Concat(quickFixResponse.QuickFixes));
        }
    }
}
