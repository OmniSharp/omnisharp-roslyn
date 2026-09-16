using System;
using System.IO;

namespace TestUtility
{
    public enum DotNetCliVersion
    {
        Current,
        Future
    }

    public static class DotNetCliVersionExtensions
    {
        private const string TestDotNetRootEnvironmentVariable = "OMNISHARP_TEST_DOTNET_ROOT";

        public static string GetFolderName(this DotNetCliVersion dotNetCliVersion)
        {
            return dotNetCliVersion switch
            {
                DotNetCliVersion.Current => ".dotnet",
                DotNetCliVersion.Future => throw new InvalidOperationException("Test infrastructure does not support a future .NET Core SDK yet."),
                _ => throw new ArgumentException($"Unknown {nameof(dotNetCliVersion)}: {dotNetCliVersion}", nameof(dotNetCliVersion)),
            };
        }

        public static string GetPath(this DotNetCliVersion dotNetCliVersion, string testAssetsRoot)
        {
            if (dotNetCliVersion == DotNetCliVersion.Current)
            {
                var configuredPath = Environment.GetEnvironmentVariable(TestDotNetRootEnvironmentVariable);
                if (!string.IsNullOrEmpty(configuredPath))
                {
                    return configuredPath;
                }
            }

            return Path.Combine(testAssetsRoot, dotNetCliVersion.GetFolderName());
        }
    }
}
