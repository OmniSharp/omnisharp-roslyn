#nullable enable

using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    public abstract class BaseTestsInContextRequest : Request
    {
        public string? RunSettings { get; set; }
        /// <summary>
        /// e.g. .NETCoreApp, Version=2.0
        /// </summary>
        public string? TargetFrameworkVersion { get; set; }
        
        public bool NoBuild { get; set; } = false;
    }
}
