using System.Composition;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Options;
using Microsoft.Extensions.Options;
using OmniSharp.Options;
using OmniSharp.Utilities;

namespace OmniSharp
{
    [Export(typeof(IOmniSharpLineFormattingOptionsProvider)), Shared]
    public class OmniSharpLineFormattingOptionsProvider : IOmniSharpLineFormattingOptionsProvider
    {
        private readonly IOptionsMonitor<OmniSharpOptions> _options;

        [ImportingConstructor]
        public OmniSharpLineFormattingOptionsProvider(IOptionsMonitor<OmniSharpOptions> options)
        {
            _options = options;
        }

        OmniSharpLineFormattingOptions IOmniSharpLineFormattingOptionsProvider.GetLineFormattingOptions()
            => _options is null
                ? new OmniSharpLineFormattingOptions()
                : CreateFromOptions(_options.CurrentValue);

        internal static OmniSharpLineFormattingOptions CreateFromOptions(OmniSharpOptions options)
            => new OmniSharpLineFormattingOptions()
                .WithProperty(nameof(OmniSharpLineFormattingOptions.IndentationSize), options.FormattingOptions.IndentationSize)
                .WithProperty(nameof(OmniSharpLineFormattingOptions.TabSize), options.FormattingOptions.TabSize)
                .WithProperty(nameof(OmniSharpLineFormattingOptions.UseTabs), options.FormattingOptions.UseTabs)
                .WithProperty(nameof(OmniSharpLineFormattingOptions.NewLine), options.FormattingOptions.NewLine);
    }
}
