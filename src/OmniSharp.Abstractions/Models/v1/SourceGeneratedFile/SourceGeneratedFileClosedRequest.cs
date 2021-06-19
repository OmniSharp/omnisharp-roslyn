#nullable enable

using OmniSharp.Mef;

namespace OmniSharp.Models.v1.SourceGeneratedFile
{
    [OmniSharpEndpoint(OmniSharpEndpoints.SourceGeneratedFileClosed, typeof(SourceGeneratedFileClosedRequest), typeof(SourceGeneratedFileClosedResponse))]
    public sealed record SourceGeneratedFileClosedRequest : SourceGeneratedFileInfo, IRequest
    {
    }
}
