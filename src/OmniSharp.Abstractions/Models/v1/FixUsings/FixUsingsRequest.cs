using OmniSharp.Mef;

﻿namespace OmniSharp.Models.FixUsings
{
    [OmniSharpEndpoint(OmniSharpEndpoints.FixUsings, typeof(FixUsingsRequest), typeof(FixUsingsResponse))]
    public class FixUsingsRequest : Request
    {
        public bool WantsTextChanges { get; set; }
        public bool ApplyTextChanges { get; set; } = true;
    }
}
