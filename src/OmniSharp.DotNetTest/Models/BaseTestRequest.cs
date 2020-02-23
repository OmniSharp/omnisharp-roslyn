using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    public class BaseTestRequest : Request
    {
        public string RunSettings { get; set; }
        public string TestFrameworkName { get; set; }
        
        /// <summary>
        /// e.g. .NETCoreApp, Version=2.0
        /// </summary>
        public string TargetFrameworkVersion { get; set; }
        public bool NoBuild { get; set; } = false;
    }

    public class SingleTestRequest : BaseTestRequest
    {
        public string MethodName { get; set; }
    }

    public class MultiTestRequest : BaseTestRequest
    {
        public string[] MethodNames { get; set; }
    }
}
