using System;
using System.Collections.Generic;
using System.Linq;

namespace OmniSharp.Models
{
    public class QuickFixResponse : IMergeableResponse
    {
        public QuickFixResponse(IEnumerable<QuickFix> quickFixes)
        {
            QuickFixes = quickFixes;
        }

        public QuickFixResponse()
        {
        }

        public IEnumerable<QuickFix> QuickFixes { get; set; }

        IMergeableResponse IMergeableResponse.Merge(IMergeableResponse response)
        {
            var quickFixResponse = (QuickFixResponse)response;
            return new QuickFixResponse(this.QuickFixes.Concat(quickFixResponse.QuickFixes));
        }
    }
}
