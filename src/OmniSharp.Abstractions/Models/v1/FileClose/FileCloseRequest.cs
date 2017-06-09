using OmniSharp.Mef;

namespace OmniSharp.Models.FileClose
{
    [OmniSharpEndpoint(OmniSharpEndpoints.Close, typeof(FileCloseRequest), typeof(FileCloseResponse))]
    public class FileCloseRequest : Request { }
}
