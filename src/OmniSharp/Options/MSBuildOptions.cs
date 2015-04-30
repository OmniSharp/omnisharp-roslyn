namespace OmniSharp.Options
{
    public class MSBuildOptions
    {
        public string ToolsVersion { get; set; }

        public string VisualStudioVersion { get; set; }
        
        // TODO: Allow loose properties
        // public IConfiguration Properties { get; set; }
    }
}