namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        public AspNet5Options AspNet5 { get; set; }
        
        public FormattingOptions FormattingOptions { get; set; }
        
        public OmniSharpOptions()
        {
            AspNet5 = new AspNet5Options();
            FormattingOptions = new FormattingOptions();
        }
    }
}