#nullable enable

using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.RunTestsInContext, typeof(RunTestsInContextRequest), typeof(RunTestResponse))]
    public class RunTestsInContextRequest : Request
    {
        public string? RunSettings { get; set; }
        public string TestFrameworkName { get; set; } = null!;
        /// <summary>
        /// e.g. .NETCoreApp, Version=2.0
        /// </summary>
        public string? TargetFrameworkVersion { get; set; }
    }
}
