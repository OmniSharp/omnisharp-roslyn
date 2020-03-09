using OmniSharp.Models;

namespace OmniSharp.DotNetTest.Models
{
    public class BaseTestClassRequest : Request
    {
        public string[] MethodNames { get; set; }
        public string RunSettings { get; set; }
        public string TestFrameworkName { get; set; }
        
        /// <summary>
        /// e.g. .NETCoreApp, Version=2.0
        /// </summary>
        public string TargetFrameworkVersion { get; set; }
    }
}
