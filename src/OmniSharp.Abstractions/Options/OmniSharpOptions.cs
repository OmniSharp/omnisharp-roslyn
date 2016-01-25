namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        public bool DotNetPrototype { get; set; }

        public FormattingOptions FormattingOptions { get; set; } = new FormattingOptions();
    }
}
