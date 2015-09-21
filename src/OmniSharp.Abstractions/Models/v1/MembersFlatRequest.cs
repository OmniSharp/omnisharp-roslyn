using System.Collections.Generic;
using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/currentfilemembersasflat", typeof(MembersFlatRequest), typeof(IEnumerable<QuickFix>))]
    public class MembersFlatRequest : Request
    {
    }
}
