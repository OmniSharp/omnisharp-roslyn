using System;

namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        public FormattingOptions FormattingOptions { get; }

        public OmniSharpOptions() : this(new FormattingOptions()) { }

        public OmniSharpOptions(FormattingOptions options)
        {
            FormattingOptions = options ?? throw new ArgumentNullException(nameof(options));
        }
    }
}
