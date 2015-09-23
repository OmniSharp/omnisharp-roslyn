using System;
using System.Composition;
using System.Threading.Tasks;

namespace OmniSharp.Mef
{
    public class EndpointDescriptor
    {
        public string EndpointName { get; set; }
        public Type RequestType { get; set; }
        public Type ResponseType { get; set; }
    }

    [MetadataAttribute]
    public class OmniSharpEndpointAttribute : ExportAttribute
    {
        public string EndpointName { get; }
        public Type RequestType { get; }
        public Type ResponseType { get; }

        public OmniSharpEndpointAttribute(string endpointName, Type requestType, Type responseType) : base(typeof(IRequest))
        {
            EndpointName = endpointName;
            RequestType = requestType;
            ResponseType = responseType;
        }
    }
}
