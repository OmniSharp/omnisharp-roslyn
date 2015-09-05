using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.ConfigurationModel;


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

        public T GetOptions<T>(T options)
        {
            IConfiguration configuration = null;
            var name = typeof(T).Name;
            if (name.EndsWith("Options")) {
                name = name.Substring(0, name.IndexOf("Options") + 1);
            }
            if (!_items.TryGetValue(name, out configuration))
            {
                throw new NotSupportedException($"No options configured for {name} are you sure it has been loaded?");
            }

            configuration.Bind(options);

            return options;
        }
    }
}
