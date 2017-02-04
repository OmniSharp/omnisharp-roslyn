using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace OmniSharp.MSBuild
{
    public static class MSBuildEnvironment
    {
        public const string MSBuildExePathName = "MSBUILD_EXE_PATH";
        public const string MSBuildExtensionsPathName = "MSBuildExtensionsPath";
        public const string MSBuildSDKsPathName = "MSBuildSDKsPath";

        private static bool s_isInitialized;
        private static string s_msbuildFolder;
        private static string s_msbuildExtensionsPath;
        private static string s_msbuildSDKsPath;

        public static bool IsInitialized => s_isInitialized;

        public static string MSBuildFolder
        {
            get
            {
                EnsureInitialized();
                return s_msbuildFolder;
            }
        }

        public static string MSBuildExtensionsPath
        {
            get
            {
                EnsureInitialized();
                return s_msbuildExtensionsPath;
            }
        }

        public static string MSBuildSDKsPath
        {
            get
            {
                EnsureInitialized();
                return s_msbuildSDKsPath;
            }
        }

        public static void Initialize(ILogger logger)
        {
            if (s_isInitialized)
            {
                throw new InvalidOperationException("MSBuildEnvironment is already initialized");
            }

            // If OmniSharp is running normally, MSBuild is located in an 'msbuild' folder beneath OmniSharp.exe.
            var msbuildFolder = Path.Combine(AppContext.BaseDirectory, "msbuild");

            if (!Directory.Exists(msbuildFolder))
            {
                // If the 'msbuild' folder does not exist beneath OmniSharp.exe, this is likely a development scenario,
                // such as debugging or running unit tests. In that case, we use one of the .msbuild-* folders at the
                // solution level.
                msbuildFolder = FindMSBuildFolderFromSolution(logger);
            }

            if (msbuildFolder == null)
            {
                logger.LogError("Could not locate MSBuild path. MSBuildProjectSystem will not function properly.");
                return;
            }

            // Set the MSBUILD_EXE_PATH environment variable to the location of MSBuild.exe or MSBuild.dll.
            var msbuildExePath = Path.Combine(msbuildFolder, "MSBuild.exe");
            if (!File.Exists(msbuildExePath))
            {
                msbuildExePath = Path.Combine(msbuildFolder, "MSBuild.dll");
            }

            if (!File.Exists(msbuildExePath))
            {
                logger.LogError("Could not locate MSBuild to set MSBUILD_EXE_PATH");
                return;
            }

            Environment.SetEnvironmentVariable(MSBuildExePathName, msbuildExePath);
            logger.LogInformation($"{MSBuildExePathName} environment variable set to {msbuildExePath}");

            // Set the MSBuildExtensionsPath environment variable to the msbuild folder.
            Environment.SetEnvironmentVariable(MSBuildExtensionsPathName, msbuildFolder);
            logger.LogInformation($"{MSBuildExtensionsPathName} environment variable set to {msbuildFolder}");

            // Set the MSBuildSDKsPath environment variable to the location of the SDKs.
            var msbuildSdksFolder = Path.Combine(msbuildFolder, "Sdks");
            if (Directory.Exists(msbuildSdksFolder))
            {
                s_msbuildSDKsPath = msbuildSdksFolder;
                Environment.SetEnvironmentVariable(MSBuildSDKsPathName, msbuildSdksFolder);
                logger.LogInformation($"{MSBuildSDKsPathName} environment variable set to {msbuildSdksFolder}");
            }
            else
            {
                logger.LogError($"Could not locate MSBuild Sdks path to set {MSBuildSDKsPathName}");
            }

            s_msbuildFolder = msbuildFolder;
            s_msbuildExtensionsPath = msbuildFolder;
            s_isInitialized = true;
        }

        private static void EnsureInitialized()
        {
            if (!s_isInitialized)
            {
                throw new InvalidOperationException("MSBuildEnvironment is not initialized");
            }
        }

        private static string FindMSBuildFolderFromSolution(ILogger logger)
        {
            // Try to locate the appropriate build-time msbuild folder by searching for
            // the OmniSharp solution relative to the current folder.

            logger.LogInformation("Searching for solution OmniSharp.sln to locate MSBuild tools...");

            var current = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(current, "OmniSharp.sln")))
            {
                current = Path.GetDirectoryName(current);
                if (Path.GetPathRoot(current) == current)
                {
                    break;
                }
            }

#if NET46
            var folderName = ".msbuild-net46";
#else
            var folderName = ".msbuild-netcoreapp1.1";
#endif

            var result = Path.Combine(current, folderName);

            return Directory.Exists(result)
                ? result
                : null;
        }
    }
}
