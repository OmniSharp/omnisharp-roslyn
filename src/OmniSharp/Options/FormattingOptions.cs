namespace OmniSharp.Options 
{
    public class FormattingOptions 
    {
        public FormattingOptions()
        {
            //just defaults
            NewLine = "\n";
            UseTabs = false;
            TabSize = 4;
            IndentationSize = 4;
        }

        public string NewLine { get; set; }
        
        public bool UseTabs { get; set; }
        
        public int TabSize { get; set; }

        public int IndentationSize { get; set; }
    }   
}