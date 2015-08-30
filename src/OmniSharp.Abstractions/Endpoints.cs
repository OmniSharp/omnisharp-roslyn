using System;
using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Mef;
using System.Composition;

namespace OmniSharp
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class EndpointNameAttribute : Attribute
    {
        public string Name { get; }
        public EndpointNameAttribute(string name)
        {
            Name = name;
        }
    }

    public static class Endpoints
    {
        public class EndpointMapItem
        {
            public EndpointMapItem(string endpointName, Type interfaceType, Type requestType, Type responseType)
            {
                EndpointName = endpointName;
                RequestType = requestType;
                ResponseType = responseType;
                InterfaceType = interfaceType;
            }

            public string EndpointName { get; }
            public Type InterfaceType {get;}
            public Type RequestType { get; }
            public Type ResponseType { get; }
        }

        public static EndpointMapItem[] Map = {
            new EndpointMapItem("/gotodefinition", typeof(GotoDefinition), typeof(GotoDefinitionRequest), typeof(GotoDefinitionResponse)),
        };

        public interface GotoDefinition
        {
            Task<GotoDefinitionResponse> GotoDefinition(GotoDefinitionRequest request);
        }
    }
}
