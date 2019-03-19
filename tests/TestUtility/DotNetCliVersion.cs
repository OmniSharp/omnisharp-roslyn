using System;
using System.IO;

namespace TestUtility
{
    public enum DotNetCliVersion
    {
        Current,
        Legacy,
        Future
    }

    public static class DotNetCliVersionExtensions
    {
        public static string GetFolderName(this DotNetCliVersion dotNetCliVersion, string rootFolder)
        {
            switch (dotNetCliVersion)
            {
                case DotNetCliVersion.Current: return null;
                case DotNetCliVersion.Legacy: return Path.Combine(rootFolder, ".dotnet-legacy", "dotnet");
                case DotNetCliVersion.Future: throw new InvalidOperationException("Test infrastructure does not support a future .NET Core SDK yet.");
                default: throw new ArgumentException($"Unknown {nameof(dotNetCliVersion)}: {dotNetCliVersion}", nameof(dotNetCliVersion));
            }
        }
    }
}
