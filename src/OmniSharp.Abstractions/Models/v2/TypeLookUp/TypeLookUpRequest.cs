using OmniSharp.Mef;
namespace OmniSharp.Models.v2.TypeLookUp
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.TypeLookup, typeof(TypeLookupRequest), typeof(TypeLookupResponse))]
    class TypeLookUpRequest:Request
    {
        public bool IncludeDocumentation { get; set; }
    }
}

