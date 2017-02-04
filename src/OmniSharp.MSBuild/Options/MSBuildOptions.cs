namespace OmniSharp.Options
{
    public class MSBuildOptions
    {
        public string ToolsVersion { get; set; }
        public string VisualStudioVersion { get; set; }
        public bool WaitForDebugger { get; set; }
        public bool EnablePackageAutoRestore { get; set; }

        public string MSBuildExtensionsPath { get; set; }
        public string MSBuildSDKsPath { get; set; }

        // TODO: Allow loose properties
        // public IConfiguration Properties { get; set; }
    }
}