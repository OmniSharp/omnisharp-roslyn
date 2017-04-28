using System;

namespace OmniSharp.Mef
{
    public class OmniSharpEndpointMetadata
    {
        public string EndpointName { get; set; }
        public Type RequestType { get; set; }
        public Type ResponseType { get; set; }

        public override string ToString()
        {
            return $"{{{nameof(EndpointName)} = {EndpointName}, {nameof(RequestType)} = {RequestType.FullName}, {nameof(ResponseType)} = {ResponseType.FullName}}}";
        }
    }
}
