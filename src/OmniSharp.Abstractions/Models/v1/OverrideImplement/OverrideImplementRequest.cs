using OmniSharp.Mef;
using System.Collections.Generic;

namespace OmniSharp.Models.v1.OverrideImplement
{
    [OmniSharpEndpoint(OmniSharpEndpoints.OverrideImplement, typeof(OverrideImplementRequest), typeof(OverrideImplementResponce))]
    public class OverrideImplementRequest : Request
    {
        public string OverrideTarget { get; set; }
    }
}
