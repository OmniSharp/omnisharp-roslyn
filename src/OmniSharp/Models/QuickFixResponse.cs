using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class QuickFixResponse
    {
        public QuickFixResponse(IEnumerable<QuickFix> quickFixes)
        {
            QuickFixes = quickFixes;
        }

        public QuickFixResponse()
        {
        }

        public IEnumerable<QuickFix> QuickFixes { get; set; }
    }
}