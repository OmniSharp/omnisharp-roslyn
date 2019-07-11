using System;

namespace TestUtility
{
    public enum DotNetCliVersion
    {
        Current,
        Future
    }

    public static class DotNetCliVersionExtensions
    {
        public static string GetFolderName(this DotNetCliVersion dotNetCliVersion)
        {
            switch (dotNetCliVersion)
            {
                case DotNetCliVersion.Current: return ".dotnet";
                case DotNetCliVersion.Future: throw new InvalidOperationException("Test infrastructure does not support a future .NET Core SDK yet.");
                default: throw new ArgumentException($"Unknown {nameof(dotNetCliVersion)}: {dotNetCliVersion}", nameof(dotNetCliVersion));
            }
        }
    }
}
