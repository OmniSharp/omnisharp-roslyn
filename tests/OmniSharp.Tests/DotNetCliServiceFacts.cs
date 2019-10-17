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
        public void GetVersion()
        {
            using (var host = CreateOmniSharpHost(dotNetCliVersion: DotNetCliVersion.Current))
            {
                var dotNetCli = host.GetExport<IDotNetCliService>();

                var version = dotNetCli.GetVersion();

                Assert.Equal(3, version.Major);
                Assert.Equal(0, version.Minor);
                Assert.Equal(100, version.Patch);
                Assert.Equal("", version.Release);
            }
        }

        [Fact]
        public void GetInfo()
        {
            using (var host = CreateOmniSharpHost(dotNetCliVersion: DotNetCliVersion.Current))
            {
                var dotNetCli = host.GetExport<IDotNetCliService>();

                var info = dotNetCli.GetInfo();

                Assert.Equal(3, info.Version.Major);
                Assert.Equal(0, info.Version.Minor);
                Assert.Equal(100, info.Version.Patch);
                Assert.Equal("", info.Version.Release);
            }
        }
    }
}
