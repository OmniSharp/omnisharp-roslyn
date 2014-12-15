using System;

namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        public AspNet5Options AspNet5 { get; set; }

        public OmniSharpOptions()
        {
            AspNet5 = new AspNet5Options();
        }
    }
}