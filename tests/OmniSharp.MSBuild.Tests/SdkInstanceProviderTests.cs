using System.IO;
using OmniSharp.MSBuild.Discovery.Providers;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.MSBuild.Tests
{
    public class SdkInstanceProviderTests : AbstractTestFixture
    {
        public SdkInstanceProviderTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void VersionString_Malformed_DoesNotParse()
        {
            var versionString = "This.Is.Not.Valid";

            var parsed = SdkInstanceProvider.TryParseVersion(
                versionString,
                out var version,
                out var errorMessage);

            Assert.False(parsed);
            Assert.NotNull(errorMessage);
        }

        [Fact]
        public void VersionString_LessThanMinimumVersion_DoesNotParse()
        {
            var versionString = "5.0.100";

            var parsed = SdkInstanceProvider.TryParseVersion(
                versionString,
                out var version,
                out var errorMessage);

            Assert.False(parsed);
            Assert.NotNull(errorMessage);
        }

        [Fact]
        public void VersionString_ReleaseVersion_DoesParse()
        {
            var versionString = "6.0.100";

            var parsed = SdkInstanceProvider.TryParseVersion(
                versionString,
                out var version,
                out var errorMessage);

            Assert.True(parsed);
            Assert.Equal(versionString, version.ToString());
            Assert.Null(errorMessage);
        }

        [Fact]
        public void VersionString_PreReleaseVersion_DoesParse()
        {
            var versionString = "7.0.100-preview.2";

            var parsed = SdkInstanceProvider.TryParseVersion(
                versionString,
                out var version,
                out var errorMessage);

            Assert.True(parsed);
            Assert.Equal(versionString, version.ToString());
            Assert.Null(errorMessage);
        }

        [Fact]
        public void MissingVersionFile_DoNotInclude()
        {
            var sdkPath = CreateFakeSdkFolder(version: null);

            var include = SdkInstanceProvider.IncludeSdkInstance(
                sdkPath,
                targetVersion: null,
                includePrerelease: false);

            Assert.False(include);
        }

        [Fact]
        public void ReleaseVersion_Include()
        {
            var sdkPath = CreateFakeSdkFolder(version: new("6.0.100"));

            var include = SdkInstanceProvider.IncludeSdkInstance(
                sdkPath,
                targetVersion: null,
                includePrerelease: false);

            Assert.True(include);
        }

        [Fact]
        public void ReleaseVersion_DoesNotMatchTargetVersion_DoNotInclude()
        {
            var sdkPath = CreateFakeSdkFolder(version: new("6.0.100"));

            var include = SdkInstanceProvider.IncludeSdkInstance(
                sdkPath,
                targetVersion: new("6.0.101"),
                includePrerelease: false);

            Assert.False(include);
        }

        [Fact]
        public void ReleaseVersion_MatchesTargetVersion_Include()
        {
            var sdkPath = CreateFakeSdkFolder(version: new("6.0.101"));

            var include = SdkInstanceProvider.IncludeSdkInstance(
                sdkPath,
                targetVersion: new("6.0.101"),
                includePrerelease: false);

            Assert.True(include);
        }

        [Fact]
        public void PreReleaseVersion_NotIncludesPrereleases_DoNotInclude()
        {
            var sdkPath = CreateFakeSdkFolder(version: new("7.0.100-preview.2"));

            var include = SdkInstanceProvider.IncludeSdkInstance(
                sdkPath,
                targetVersion: null,
                includePrerelease: false);

            Assert.False(include);
        }

        [Fact]
        public void PreReleaseVersion_IncludesPrereleases_Include()
        {
            var sdkPath = CreateFakeSdkFolder(version: new("7.0.100-preview.2"));

            var include = SdkInstanceProvider.IncludeSdkInstance(
                sdkPath,
                targetVersion: null,
                includePrerelease: true);

            Assert.True(include);
        }

        [Fact]
        public void PreReleaseVersion_TargetVersionTrumpsIncludePrereleases_Include()
        {
            var sdkPath = CreateFakeSdkFolder(version: new("7.0.100-preview.2"));

            var include = SdkInstanceProvider.IncludeSdkInstance(
                sdkPath,
                targetVersion: new("7.0.100-preview.2"),
                includePrerelease: false);

            Assert.True(include);
        }

        [Fact]
        public void SdkPath_PathDoesNotExist_DoesNotGetVersion()
        {
            var sdkPath = "ThisPathDoesNotExist";

            var got = SdkOverrideInstanceProvider.TryGetVersion(
                sdkPath,
                out var version,
                out var errorString);

            Assert.False(got);
            Assert.NotNull(errorString);
        }

        [Fact]
        public void SdkPath_VersionFileDoesNotExist_DoesNotGetVersion()
        {
            var sdkPath = CreateFakeSdkFolder(version: null);

            var got = SdkOverrideInstanceProvider.TryGetVersion(
                sdkPath,
                out var version,
                out var errorString);

            Assert.False(got);
            Assert.NotNull(errorString);
        }

        [Fact]
        public void SdkPath_LessThanMinimumVersion_DoesNotGetVersion()
        {
            var versionString = "5.0.100";
            var sdkPath = CreateFakeSdkFolder(version: new(versionString));

            var got = SdkOverrideInstanceProvider.TryGetVersion(
                sdkPath,
                out var version,
                out var errorString);

            Assert.False(got);
            Assert.NotNull(errorString);
        }

        [Fact]
        public void SdkPath_ReleaseVersion_DoesGetVersion()
        {
            var versionString = "6.0.100";
            var sdkPath = CreateFakeSdkFolder(version: new(versionString));

            var got = SdkOverrideInstanceProvider.TryGetVersion(
                sdkPath,
                out var version,
                out var errorString);

            Assert.Null(errorString);
            Assert.True(got);
            Assert.Equal(versionString, version.ToString());
        }

        [Fact]
        public void SdkPath_PreReleaseVersion_DoesGetVersion()
        {
            var versionString = "7.0.100-preview.2";
            var sdkPath = CreateFakeSdkFolder(version: new(versionString));

            var got = SdkOverrideInstanceProvider.TryGetVersion(
                sdkPath,
                out var version,
                out var errorString);

            Assert.Null(errorString);
            Assert.True(got);
            Assert.Equal(versionString, version.ToString());
        }

        internal string CreateFakeSdkFolder(SemanticVersion version)
        {
            var tempFolderPath = TestIO.GetRandomTempFolderPath();

            if (version is null)
            {
                return tempFolderPath;
            }

            var versionFilePath = Path.Combine(tempFolderPath, ".version");
            File.WriteAllText(versionFilePath, version.ToString());

            return tempFolderPath;
        }
    }
}
