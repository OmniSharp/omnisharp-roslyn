using System;
using System.Composition;
using System.IO;
using OmniSharp.Services;

namespace OmniSharp.MSBuild
{
    [Export, Shared]
    public class SdksPathResolver
    {
        private const string MSBuildSDKsPath = nameof(MSBuildSDKsPath);
        private const string Sdks = nameof(Sdks);

        private readonly DotNetCliService _dotNetCli;

        [ImportingConstructor]
        public SdksPathResolver(DotNetCliService dotNetCli)
        {
            _dotNetCli = dotNetCli;
        }

        public bool TryGetSdksPath(string projectFilePath, out string sdksPath)
        {
            var projectFileDirectory = Path.GetDirectoryName(projectFilePath);

            var info = _dotNetCli.GetInfo(projectFileDirectory);

            if (info.IsEmpty || string.IsNullOrWhiteSpace(info.BasePath))
            {
                sdksPath = null;
                return false;
            }

            sdksPath = Path.Combine(info.BasePath, Sdks);

            if (Directory.Exists(sdksPath))
            {
                return true;
            }

            sdksPath = null;
            return false;
        }

        public IDisposable SetSdksPathEnvironmentVariable(string projectFilePath)
        {
            if (!TryGetSdksPath(projectFilePath, out var sdksPath))
            {
                return NullDisposable.Instance;
            }

            var oldMSBuildSDKsPath = Environment.GetEnvironmentVariable(MSBuildSDKsPath);
            Environment.SetEnvironmentVariable(MSBuildSDKsPath, sdksPath);

            return new ResetSdksPathEnvironmentVariable(oldMSBuildSDKsPath);
        }

        private class NullDisposable : IDisposable
        {
            public static IDisposable Instance { get; } = new NullDisposable();

            public void Dispose() { }
        }

        private class ResetSdksPathEnvironmentVariable : IDisposable
        {
            private readonly string _oldValue;

            public ResetSdksPathEnvironmentVariable(string oldValue)
            {
                _oldValue = oldValue;
            }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable(MSBuildSDKsPath, _oldValue);
            }
        }
    }
}
