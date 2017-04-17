using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.MembersTree, typeof(MembersTreeRequest), typeof(FileMemberTree))]
    public class MembersTreeRequest : Request
    {
    }
}
