using OmniSharp.Mef;

namespace OmniSharp.Models.GotoDefinition
{
    [OmniSharpEndpoint(OmniSharpEndpoints.GotoDefinition, typeof(GotoDefinitionRequest), typeof(GotoDefinitionResponse))]
    public class GotoDefinitionRequest : Request
    {
        public int Timeout { get; set; } = 2000;
        public bool WantMetadata { get; set; }
    }
}
