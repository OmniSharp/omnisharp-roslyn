using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Framework.Configuration;


namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        private IDictionary<string, IConfiguration> _items = new Dictionary<string, IConfiguration>();
        public OmniSharpOptions()
        {
            FormattingOptions = new FormattingOptions();
        }

        public FormattingOptions FormattingOptions { get; set; }
    }
}
