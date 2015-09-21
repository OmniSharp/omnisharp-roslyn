using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/findimplementations", typeof(FindImplementationsRequest), typeof(QuickFixResponse))]
    public class FindImplementationsRequest : Request
    {
    }
}
