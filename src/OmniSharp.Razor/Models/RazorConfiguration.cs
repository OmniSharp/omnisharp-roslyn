namespace OmniSharp.Razor.Models
{
    public class RazorConfiguration
    {
        public RazorConfiguration(/*ProjectContext context,*/ string configuration)
        {
            Name = configuration;

            /*var outputPaths = context.GetOutputPaths(configuration);
            this.CompilationOutputPath = outputPaths.CompilationOutputPath;
            this.CompilationOutputAssemblyFile = outputPaths.CompilationFiles.Assembly;
            this.CompilationOutputPdbFile = outputPaths.CompilationFiles.PdbPath;

            var compilationOptions = context.ProjectFile.GetCompilerOptions(targetFramework: context.TargetFramework, configurationName: configuration);
            this.EmitEntryPoint = compilationOptions.EmitEntryPoint;*/
        }

        public string Name { get; }
        public string CompilationOutputPath { get; }
        public string CompilationOutputAssemblyFile { get; }
        public string CompilationOutputPdbFile { get; }
        public bool? EmitEntryPoint { get; set; }
    }
}
