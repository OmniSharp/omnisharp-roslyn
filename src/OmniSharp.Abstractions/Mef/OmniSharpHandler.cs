using System;
using System.Composition;
using System.Threading.Tasks;

namespace OmniSharp.Mef
{
    public interface IRequestHandler { }

    public class OmniSharpLanguage
    {
        public string EndpointName { get; set; }
        public string Language { get; set; }
    }

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
