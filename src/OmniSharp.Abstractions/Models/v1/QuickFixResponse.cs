using System;
using System.Collections.Generic;
using System.Linq;

namespace OmniSharp.Models
{
    public class QuickFixResponse : IAggregateResponse
    {
        public QuickFixResponse(IEnumerable<QuickFix> quickFixes)
        {
            QuickFixes = quickFixes;
        }

        public QuickFixResponse()
        {
        }

        public IEnumerable<QuickFix> QuickFixes { get; set; }

        IAggregateResponse IAggregateResponse.Merge(IAggregateResponse response)
        {
            var quickFixResponse = (QuickFixResponse)response;
            return new QuickFixResponse(this.QuickFixes.Concat(quickFixResponse.QuickFixes));
        }
    }
}
