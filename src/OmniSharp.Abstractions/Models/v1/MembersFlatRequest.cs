using System.Collections.Generic;
using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.MembersFlat, typeof(MembersFlatRequest), typeof(IEnumerable<QuickFix>))]
    public class MembersFlatRequest : Request
    {
    }
}
