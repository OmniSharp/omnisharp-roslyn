using OmniSharp.Mef;

namespace OmniSharp.Models.v2.TypeLookUp
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.TypeLookup, typeof(TypeLookupRequest), typeof(TypeLookupResponse))]
    public class TypeLookupRequest : Request
    {
        public bool IncludeDocumentation { get; set; }
    }
}

