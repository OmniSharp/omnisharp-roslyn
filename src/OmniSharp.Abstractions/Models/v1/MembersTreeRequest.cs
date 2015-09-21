using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/currentfilemembersastree", typeof(MembersTreeRequest), typeof(FileMemberTree))]
    public class MembersTreeRequest : Request
    {
    }
}
