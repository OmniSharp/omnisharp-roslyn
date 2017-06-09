using OmniSharp.Mef;

namespace OmniSharp.Models.PackageSource
{
    [OmniSharpEndpoint(OmniSharpEndpoints.PackageSource, typeof(PackageSourceRequest), typeof(PackageSourceResponse))]
    public class PackageSourceRequest : IRequest
    {
        /// <summary>
        /// The path to the project file
        /// </summary>
        public string ProjectPath { get; set; }
    }
}
