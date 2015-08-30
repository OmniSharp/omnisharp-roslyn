using System;
using System.Threading.Tasks;
using OmniSharp.Models;

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
        public static EndpointMapItem[] AvailableEndpoints = {
            EndpointMapItem.Create<GotoDefinition, GotoDefinitionRequest, GotoDefinitionResponse>("/gotodefinition"),
        };

        public interface GotoDefinition
        {
            Task<GotoDefinitionResponse> GotoDefinition(GotoDefinitionRequest request);
        }

        public class EndpointMapItem
        {
            public static EndpointMapItem Create<TInterface, TRequest, TResponse>(string endpoint)
            {
                return new EndpointMapItem(endpoint, typeof(TInterface), typeof(TRequest), typeof(TResponse));
            }

            public EndpointMapItem(string endpointName, Type interfaceType, Type requestType, Type responseType)
            {
                EndpointName = endpointName;
                RequestType = requestType;
                ResponseType = responseType;
                InterfaceType = interfaceType;
            }

            public string EndpointName { get; }
            public Type InterfaceType { get; }
            public Type RequestType { get; }
            public Type ResponseType { get; }
        }
    }
}
