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

        [ConditionalFact(typeof(IsLegacyTest))]
        public void LegacyGetVersion()
        {
            using (var host = CreateOmniSharpHost(dotNetCliVersion: DotNetCliVersion.Legacy))
            {
                var dotNetCli = host.GetExport<IDotNetCliService>();

                var version = dotNetCli.GetVersion();

                Assert.Equal(1, version.Major);
                Assert.Equal(0, version.Minor);
                Assert.Equal(0, version.Patch);
                Assert.Equal("preview2-1-003177", version.Release);
            }
        }

        [ConditionalFact(typeof(IsLegacyTest))]
        public void LegacyGetInfo()
        {
            using (var host = CreateOmniSharpHost(dotNetCliVersion: DotNetCliVersion.Legacy))
            {
                var dotNetCli = host.GetExport<IDotNetCliService>();

                var info = dotNetCli.GetInfo();

                Assert.Equal(1, info.Version.Major);
                Assert.Equal(0, info.Version.Minor);
                Assert.Equal(0, info.Version.Patch);
                Assert.Equal("preview2-1-003177", info.Version.Release);
            }
        }

        [Fact]
        public void GetVersion()
        {
            using (var host = CreateOmniSharpHost(dotNetCliVersion: DotNetCliVersion.Current))
            {
                var dotNetCli = host.GetExport<IDotNetCliService>();

                var version = dotNetCli.GetVersion();

                Assert.Equal(2, version.Major);
                Assert.Equal(1, version.Minor);
                Assert.Equal(505, version.Patch);
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

                Assert.Equal(2, info.Version.Major);
                Assert.Equal(1, info.Version.Minor);
                Assert.Equal(505, info.Version.Patch);
                Assert.Equal("", info.Version.Release);
            }
        }
    }
}
