using OmniSharp.Mef;

namespace OmniSharp.Models.MembersTree
{
    [OmniSharpEndpoint(OmniSharpEndpoints.MembersTree, typeof(MembersTreeRequest), typeof(FileMemberTree))]
    public class MembersTreeRequest : Request
    {
    }
}
