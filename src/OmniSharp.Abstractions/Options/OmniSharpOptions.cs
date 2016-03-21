using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        private IDictionary<string, IConfiguration> _items = new Dictionary<string, IConfiguration>();

        public OmniSharpOptions() : this(new FormattingOptions()) { }

        public OmniSharpOptions(FormattingOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            
            FormattingOptions = options;
        }

        public FormattingOptions FormattingOptions { get; }
    }
}
