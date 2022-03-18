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
            return dotNetCliVersion switch
            {
                DotNetCliVersion.Current => ".dotnet",
                DotNetCliVersion.Future => throw new InvalidOperationException("Test infrastructure does not support a future .NET Core SDK yet."),
                _ => throw new ArgumentException($"Unknown {nameof(dotNetCliVersion)}: {dotNetCliVersion}", nameof(dotNetCliVersion)),
            };
        }
    }
}
