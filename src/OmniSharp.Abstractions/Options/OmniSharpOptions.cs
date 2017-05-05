using System;

namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        public CodeActionOptions CodeActions { get; set; } = new CodeActionOptions();

        public FormattingOptions FormattingOptions { get; }

        public OmniSharpOptions() : this(new FormattingOptions()) { }

        public OmniSharpOptions(FormattingOptions options)
        {
            FormattingOptions = options ?? throw new ArgumentNullException(nameof(options));
        }
    }
}
