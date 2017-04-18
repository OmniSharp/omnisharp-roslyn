using OmniSharp.Mef;

namespace OmniSharp.Models.FindUsages
{
    [OmniSharpEndpoint(OmniSharpEndpoints.FindUsages, typeof(FindUsagesRequest), typeof(QuickFixResponse))]
    public class FindUsagesRequest : Request
    {
        /// <summary>
        /// Only search for references in the current file
        /// </summary>
        public bool OnlyThisFile { get; set; }
        public bool ExcludeDefinition { get; set; }
    }
}
