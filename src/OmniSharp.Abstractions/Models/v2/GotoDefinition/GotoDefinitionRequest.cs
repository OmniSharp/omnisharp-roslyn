using OmniSharp.Mef;

namespace OmniSharp.Models.V2.GotoDefinition
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.GotoDefinition, typeof(GotoDefinitionRequest), typeof(GotoDefinitionResponse))]
    public class GotoDefinitionRequest : Request
    {
        public int Timeout { get; init; } = 10000;
        public bool WantMetadata { get; init; }
    }
}
