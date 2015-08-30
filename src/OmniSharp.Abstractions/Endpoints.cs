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

    public static class EndpointDelegates
    {
        [EndpointName("/gotodefinition")]
        public class GotoDefinitionAttribute : OmniSharpEndpointAttribute
        {
            public GotoDefinitionAttribute(string language) : base(typeof(Func<GotoDefinitionRequest, Task<GotoDefinitionResponse>>), language) { }
        }
    }

    public static class Endpoints
    {
        public interface GotoDefinition
        {
            Task<GotoDefinitionResponse> GotoDefinition(GotoDefinitionRequest request);
        }

        [EndpointName("/gotodefinition")]
        public class GotoDefinitionAttribute : OmniSharpEndpointAttribute
        {
            public GotoDefinitionAttribute(string language) : base(typeof(GotoDefinition), language) { }
        }
    }
}
