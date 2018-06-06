using OmniSharp.Mef;

namespace OmniSharp.Models.v2
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.BlockStructure, typeof(BlockStructureRequest), typeof(BlockStructureResponse))]
    public class BlockStructureRequest : SimpleFileRequest
    {
    }
}
