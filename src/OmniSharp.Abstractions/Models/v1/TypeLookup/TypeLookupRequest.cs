using OmniSharp.Mef;

﻿namespace OmniSharp.Models.TypeLookup
{
    [OmniSharpEndpoint(OmniSharpEndpoints.TypeLookup, typeof(TypeLookupRequest), typeof(TypeLookupResponse))]
    public class TypeLookupRequest : Request
    {
        public bool IncludeDocumentation { get; set; }
    }
}
