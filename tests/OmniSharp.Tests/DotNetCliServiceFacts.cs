using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Tests
{
    public class DotNetCliServiceFacts : AbstractTestFixture
    {
        public DotNetCliServiceFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void LegacyGetVersion()
        {
            using (var host = CreateOmniSharpHost(useLegacyDotNetCli: true))
            {
                var dotNetCli = host.GetExport<DotNetCliService>();

                var version = dotNetCli.GetVersion();

                Assert.Equal(1, version.Major);
                Assert.Equal(0, version.Minor);
                Assert.Equal(0, version.Patch);
                Assert.Equal("preview2-1-003177", version.Release);
            }
        }

        [Fact]
        public void GetVersion()
        {
            using (var host = CreateOmniSharpHost(useLegacyDotNetCli: false))
            {
                var dotNetCli = host.GetExport<DotNetCliService>();

                var version = dotNetCli.GetVersion();

                Assert.Equal(1, version.Major);
                Assert.Equal(0, version.Minor);
                Assert.Equal(1, version.Patch);
                Assert.Equal("", version.Release);
            }
        }
    }
}
