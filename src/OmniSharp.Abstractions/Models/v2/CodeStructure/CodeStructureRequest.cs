using OmniSharp.Mef;

namespace OmniSharp.Models.V2.CodeStructure
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.CodeStructure, typeof(CodeStructureRequest), typeof(CodeStructureResponse))]
    public class CodeStructureRequest : SimpleFileRequest
    {
    }
}
