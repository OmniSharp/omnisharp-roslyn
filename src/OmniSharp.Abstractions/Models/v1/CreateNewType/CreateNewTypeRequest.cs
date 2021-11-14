using OmniSharp.Mef;

namespace OmniSharp.Models.CreateNewTypeRequest
{
    /// <summary>
    /// Create new type (class/interface) in specified path and put namespace according to file path.
    /// </summary>
    [OmniSharpEndpoint(OmniSharpEndpoints.CreateNewType, typeof(CreateNewTypeRequest), typeof(CreateNewTypeResponse))]
    public class CreateNewTypeRequest : Request
    {
        /// <summary>
        /// New file absolute parent path.
        /// </summary>
        public string FileParentPath { get; set; }

        /// <summary>
        /// Creating type.
        /// </summary>
        public TypeEnum Type { get; set; }

        /// <summary>
        /// Name for new symbol.
        /// </summary>
        public string SymbolName { get; set; }
    }
}
