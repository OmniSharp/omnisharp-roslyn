using System.Composition;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Options;
using Microsoft.Extensions.Options;
using OmniSharp.Options;

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
            => new()
            {
                IndentationSize = options.FormattingOptions.IndentationSize,
                TabSize = options.FormattingOptions.TabSize,
                UseTabs = options.FormattingOptions.UseTabs,
                NewLine = options.FormattingOptions.NewLine,
            };
    }
}
