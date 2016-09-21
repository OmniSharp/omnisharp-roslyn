namespace OmniSharp.Options
{
    public class MSBuildOptions
    {
        public string ToolsVersion { get; set; }
        public string VisualStudioVersion { get; set; }
        public bool WaitForDebugger { get; set; }
        public string MSBuildExtensionsPath { get; set; }

        // TODO: Allow loose properties
        // public IConfiguration Properties { get; set; }
    }
}