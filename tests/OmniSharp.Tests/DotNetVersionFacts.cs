using System.Collections.Generic;
using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Tests
{
    public class DotNetVersionFacts : AbstractTestFixture
    {
        public DotNetVersionFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [InlineData("6.0.201")]
        [InlineData("7.0.100-preview.2.22153.17")]
        public void ParseVersion(string versionString)
        {
            var cliVersion = DotNetVersion.Parse(new() { versionString });

            Assert.False(cliVersion.HasError, $"{versionString} did not successfully parse.");

            Assert.Equal(versionString, cliVersion.Version.ToString());
        }

        [Fact]
        public void ParseErrorMessage()
        {
            const string RequestedSdkVersion = "6.0.301-rtm.22263.15";
            const string GlobalJsonFile = "/Users/username/Source/format/global.json";
            const string ExpectedErrorMessage = $"Install the [{RequestedSdkVersion}] .NET SDK or update [{GlobalJsonFile}] to match an installed SDK.";

            var lines = new List<string>() {
                "The command could not be loaded, possibly because:",
                "  * You intended to execute a .NET application:",
                "      The application '--version' does not exist.",
                "  * You intended to execute a .NET SDK command:",
                "      A compatible .NET SDK was not found.",
                "",
                $"Requested SDK version: {RequestedSdkVersion}",
                $"global.json file: {GlobalJsonFile}",
                "",
                "Installed SDKs:",
                "6.0.105 [/usr/local/share/dotnet/sdk]",
                "6.0.202 [/usr/local/share/dotnet/sdk]",
                "6.0.400 [/usr/local/share/dotnet/sdk]",
                "7.0.100-rc.1.22431.12 [/usr/local/share/dotnet/sdk]",
                "",
                $"Install the [{RequestedSdkVersion}] .NET SDK or update [{GlobalJsonFile}] to match an installed SDK.",
                "",
                "Learn about SDK resolution:",
                "https://aka.ms/dotnet/sdk-not-found"
            };

            var cliVersion = DotNetVersion.Parse(lines);

            Assert.True(cliVersion.HasError);

            Assert.Equal(ExpectedErrorMessage, cliVersion.ErrorMessage);
        }
    }
}
