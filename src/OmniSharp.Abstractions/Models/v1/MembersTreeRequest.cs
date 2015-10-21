using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint(OmnisharpEndpoints.MembersTree, typeof(MembersTreeRequest), typeof(FileMemberTree))]
    public class MembersTreeRequest : Request
    {
    }
}
