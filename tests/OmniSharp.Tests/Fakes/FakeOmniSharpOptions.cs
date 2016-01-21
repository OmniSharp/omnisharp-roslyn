using Microsoft.Extensions.Options;
using OmniSharp.Options;

namespace OmniSharp.Tests
{
    public class FakeOmniSharpOptions : IOptions<OmniSharpOptions>
    {
        public OmniSharpOptions Options { get; set; }

        public OmniSharpOptions Value => new OmniSharpOptions();
    }
}
