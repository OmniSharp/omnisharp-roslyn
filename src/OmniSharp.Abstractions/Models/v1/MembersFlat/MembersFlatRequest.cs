using System.Collections.Generic;
using OmniSharp.Mef;

namespace OmniSharp.Models.MembersFlat
{
    [OmniSharpEndpoint(OmniSharpEndpoints.MembersFlat, typeof(MembersFlatRequest), typeof(IEnumerable<QuickFix>))]
    public class MembersFlatRequest : Request
    {
    }
}
