using OmniSharp.Mef;

namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/navigateup", typeof(NavigateUpRequest), typeof(NavigateResponse))]
    public class NavigateUpRequest : Request
    {
    }
}
