namespace OmniSharp.Options
{
    public class MSBuildOptions
    {
        public string Configuration { get; set; }
        public string Platform { get; set; }
        public bool EnablePackageAutoRestore { get; set; }

        /// <summary>
        /// If true, MSBuild project system will only be loading projects for files that were opened in the editor
        /// as well as referenced projects, recursively.
        /// </summary>
        public bool LoadProjectsOnDemand { get; set; }

    }
}
