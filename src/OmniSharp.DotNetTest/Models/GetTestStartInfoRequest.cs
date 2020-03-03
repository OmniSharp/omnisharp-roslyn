using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.GetTestStartInfo, typeof(GetTestStartInfoRequest), typeof(GetTestStartInfoResponse))]
    public class GetTestStartInfoRequest : Request
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
