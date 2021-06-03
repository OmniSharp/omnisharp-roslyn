using NuGet.Versioning;
using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Tests
{
    public class DotNetCliServiceFacts : AbstractTestFixture
    {
        private const string DotNetVersion = "5.0.300";
        private int Major { get; }
        private int Minor { get; }
        private int Patch { get; }
        private string Release { get; }

        public DotNetCliServiceFacts(ITestOutputHelper output)
            : base(output)
        {
            var version = SemanticVersion.Parse(DotNetVersion);
            Major = version.Major;
            Minor = version.Minor;
            Patch = version.Patch;
            Release = version.Release;
        }

        [Fact]
        public void GetVersion()
        {
            using (var host = CreateOmniSharpHost(dotNetCliVersion: DotNetCliVersion.Current))
            {
                var dotNetCli = host.GetExport<IDotNetCliService>();

                var version = dotNetCli.GetVersion();

                Assert.Equal(Major, version.Major);
                Assert.Equal(Minor, version.Minor);
                Assert.Equal(Patch, version.Patch);
                Assert.Equal(Release, version.Release);
            }
        }

        [Fact]
        public void GetInfo()
        {
            using (var host = CreateOmniSharpHost(dotNetCliVersion: DotNetCliVersion.Current))
            {
                var dotNetCli = host.GetExport<IDotNetCliService>();

                var info = dotNetCli.GetInfo();

                Assert.Equal(Major, info.Version.Major);
                Assert.Equal(Minor, info.Version.Minor);
                Assert.Equal(Patch, info.Version.Patch);
                Assert.Equal(Release, info.Version.Release);
            }
        }
    }
}
