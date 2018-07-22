using OmniSharp.Mef;

namespace OmniSharp.Models.V2
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.BlockStructure, typeof(BlockStructureRequest), typeof(BlockStructureResponse))]
    public class BlockStructureRequest : SimpleFileRequest
    {
    }
}
