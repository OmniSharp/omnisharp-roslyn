using System.Composition;

namespace OmniSharp.Mef
{
    [MetadataAttribute]
    public class OmniSharpHandlerAttribute : ExportAttribute
    {
        public string Language { get; }

        public string EndpointName { get; }

        public OmniSharpHandlerAttribute(string endpoint, string language) : base(typeof(IRequestHandler))
        {
            EndpointName = endpoint;
            Language = language;
        }
    }
}
