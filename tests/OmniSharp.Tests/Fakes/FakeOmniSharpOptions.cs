using Microsoft.Extensions.OptionsModel;
using OmniSharp.Options;

namespace OmniSharp.Tests
{
    public class FakeOmniSharpOptions : IOptions<OmniSharpOptions>
    {
        public OmniSharpOptions Value => new OmniSharpOptions();
    }
}
