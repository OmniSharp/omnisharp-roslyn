using System.IO;

namespace OmniSharp.Script
{
    public class ScriptOptions
    {
        public bool EnableScriptNuGetReferences { get; set; }

        public string DefaultTargetFramework { get; set; } = "net46";

        /// <summary>  
        ///  Nuget for scripts is enabled when <see cref="EnableScriptNuGetReferences"/> is enabled or when <see cref="DefaultTargetFramework"/> is .NET Core
        /// </summary>  
        public bool IsNugetEnabled() =>
            EnableScriptNuGetReferences ||
            (DefaultTargetFramework != null && DefaultTargetFramework.StartsWith("netcoreapp", System.StringComparison.OrdinalIgnoreCase));

        public string RspFilePath { get; set; }

        public string GetNormalizedRspFilePath(IOmniSharpEnvironment env)
        {
            if (string.IsNullOrWhiteSpace(RspFilePath)) return null;
            return Path.IsPathRooted(RspFilePath)
                ? RspFilePath
                : Path.Combine(env.TargetDirectory, RspFilePath);
        }
    }
}
