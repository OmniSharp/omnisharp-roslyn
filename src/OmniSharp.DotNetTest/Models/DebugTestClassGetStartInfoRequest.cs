using OmniSharp.Mef;
using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    [OmniSharpEndpoint(OmniSharpEndpoints.V2.DebugTestsInClassGetStartInfo, typeof(DebugTestClassGetStartInfoRequest), typeof(DebugTestGetStartInfoResponse[]))]
    class DebugTestClassGetStartInfoRequest :  Request
    {
        public string[] MethodsInClass { get; set; }
        public string TestFrameworkName { get; set; }
        /// <summary>
        /// e.g. .NETCoreApp, Version=2.0
        /// </summary>
        public string TargetFrameworkVersion { get; set; }
    }
}
