namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        public AspNet5Options AspNet5 { get; set; }
        
        public MSBuildOptions MsBuild { get; set; }

        public FormattingOptions FormattingOptions { get; set; }
        
        public OmniSharpOptions()
        {
            AspNet5 = new AspNet5Options();
            MsBuild = new MSBuildOptions();
            FormattingOptions = new FormattingOptions();
        }
    }
}