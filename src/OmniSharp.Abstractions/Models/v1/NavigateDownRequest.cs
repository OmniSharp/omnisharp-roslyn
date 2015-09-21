using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/navigatedown", typeof(NavigateDownRequest), typeof(NavigateResponse))]
    public class NavigateDownRequest : Request
    {
    }
}
