using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.DebugTestGetStartInfo, typeof(DebugTestGetStartInfoRequest), typeof(DebugTestGetStartInfoResponse))]
    public class DebugTestGetStartInfoRequest : Request
    {
        public string MethodName { get; set; }
        public string RunSettings { get; set; }
        public string TestFrameworkName { get; set; }
        /// <summary>
        /// e.g. .NETCoreApp, Version=2.0
        /// </summary>
        public string TargetFrameworkVersion { get; set; }
    }
}
