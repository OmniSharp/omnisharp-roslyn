using OmniSharp.Mef;

namespace OmniSharp.Models.GotoTypeDefinition
{
    [OmniSharpEndpoint(OmniSharpEndpoints.GotoTypeDefinition, typeof(GotoTypeDefinitionRequest), typeof(GotoTypeDefinitionResponse))]
    public class GotoTypeDefinitionRequest : Request
    {
        public int Timeout { get; init; } = 10000;
        public bool WantMetadata { get; init; }
    }
}
