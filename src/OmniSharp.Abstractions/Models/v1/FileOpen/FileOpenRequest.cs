using OmniSharp.Mef;

namespace OmniSharp.Models.FileOpen
{
    [OmniSharpEndpoint(OmniSharpEndpoints.Open, typeof(FileOpenRequest), typeof(FileOpenResponse))]
    public class FileOpenRequest : Request { }
}
