using System.Collections.Generic;

namespace OmniSharp.Models
{
    public class FixUsingsResponse
    {
        public FixUsingsResponse(string buffer, IEnumerable<QuickFix> ambiguous)
        {
            Buffer = buffer;
            AmbiguousResults = ambiguous;
        }

        public string Buffer { get; private set; }
        public IEnumerable<QuickFix> AmbiguousResults { get; private set; }
    }
}
