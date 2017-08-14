namespace OmniSharp.Options
{
    public class MSBuildOptions
    {
        public string ToolsVersion { get; set; }
        public string VisualStudioVersion { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }
        public bool EnablePackageAutoRestore { get; set; }

        public string MSBuildExtensionsPath { get; set; }
        public string TargetFrameworkRootPath { get; set; }
        public string MSBuildSDKsPath { get; set; }
        public string RoslynTargetsPath { get; set; }
        public string CscToolPath { get; set; }
        public string CscToolExe { get; set; }

        // TODO: Allow loose properties
        // public IConfiguration Properties { get; set; }
    }
}
