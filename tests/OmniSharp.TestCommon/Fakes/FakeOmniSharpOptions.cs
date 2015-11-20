using Microsoft.Extensions.OptionsModel;
using OmniSharp.Options;

namespace OmniSharp.TestCommon
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
