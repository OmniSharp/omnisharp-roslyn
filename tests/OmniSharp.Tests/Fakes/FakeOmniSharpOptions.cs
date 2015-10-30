using Microsoft.Framework.OptionsModel;
using OmniSharp.Options;

namespace OmniSharp.Tests
{
    public class FakeOmniSharpOptions : IOptions<OmniSharpOptions>
    {
        public OmniSharpOptions Value { get; set; }
        public OmniSharpOptions GetNamedOptions(string name)
        {
            return new OmniSharpOptions();
        }
    }
}
