using Microsoft.Extensions.Options;
using OmniSharp.Options;

namespace TestUtility.Fake
{
    public class FakeOmniSharpOptions : IOptions<OmniSharpOptions>
    {
        public OmniSharpOptions Options { get; set; }

        public OmniSharpOptions Value => new OmniSharpOptions();
    }
}
