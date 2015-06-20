namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        public DnxOptions Dnx { get; set; }
        
        public MSBuildOptions MsBuild { get; set; }

        public FormattingOptions FormattingOptions { get; set; }
        
        public OmniSharpOptions()
        {
            Dnx = new DnxOptions();
            MsBuild = new MSBuildOptions();
            FormattingOptions = new FormattingOptions();
        }
    }
}