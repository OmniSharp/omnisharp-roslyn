namespace OmniSharp.Options
{
    internal class MSBuildOptions
    {
        public string ToolsVersion { get; set; }
        public string VisualStudioVersion { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }
        public bool EnablePackageAutoRestore { get; set; }

        /// <summary>
        /// When set to true, the MSBuild project system will attempt to resolve the path to the MSBuild
        /// SDKs for a project by running 'dotnet --info' and retrieving the path. This is only needed
        /// for older versions of the .NET Core SDK.
        /// </summary>
        public bool UseLegacySdkResolver { get; set; }

        public string MSBuildExtensionsPath { get; set; }
        public string TargetFrameworkRootPath { get; set; }
        public string MSBuildSDKsPath { get; set; }
        public string RoslynTargetsPath { get; set; }
        public string CscToolPath { get; set; }
        public string CscToolExe { get; set; }

        /// <summary>
        /// When set to true, the MSBuild project system will generate binary logs for each project that
        /// it loads.
        /// </summary>
        public bool GenerateBinaryLogs { get; set; }

        // TODO: Allow loose properties
        // public IConfiguration Properties { get; set; }
    }
}
