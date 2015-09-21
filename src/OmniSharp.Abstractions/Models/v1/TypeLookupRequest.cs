using OmniSharp.Mef;

﻿namespace OmniSharp.Models
{
    [OmniSharpEndpoint("/typelookup", typeof(TypeLookupRequest), typeof(TypeLookupResponse))]
    public class TypeLookupRequest : Request
    {
        public bool IncludeDocumentation { get; set; }
    }
}
