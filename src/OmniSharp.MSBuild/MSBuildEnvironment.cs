using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace OmniSharp.MSBuild
{
    public static class MSBuildEnvironment
    {
        private static bool s_isInitialized;
        private static string s_msbuildFolder;

        public static bool IsInitialized => s_isInitialized;

        public static string MSBuildFolder
        {
            get
            {
                EnsureInitialized();
                return s_msbuildFolder;
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
                msbuildFolder = FindMSBuildFolderFromSolution();
            }

            if (msbuildFolder == null || !Directory.Exists(msbuildFolder))
            {
                logger.LogError("Could not locate MSBuild path. MSBuildProjectSystem will not function properly.");
                return;
            }

            // Set the MSBuildExtensionsPath environment variable to the msbuild folder.
            Environment.SetEnvironmentVariable("MSBuildExtensionsPath", msbuildFolder);
            logger.LogInformation($"MSBuildExtensionsPath environment variable set to {msbuildFolder}");

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

            if (File.Exists(msbuildExePath))
            {
                Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", msbuildExePath);
                logger.LogInformation($"MSBUILD_EXE_PATH environment variable set to {msbuildExePath}");
            }
            else
            {
                logger.LogError("Could not locate MSBuild to set MSBUILD_EXE_PATH"); ;
            }

            // Set the MSBuildSDKsPath environment variable to the location of the SDKs.
            var msbuildSdksFolder = Path.Combine(msbuildFolder, "Sdks");
            if (Directory.Exists(msbuildSdksFolder))
            {
                Environment.SetEnvironmentVariable("MSBuildSDKsPath", msbuildSdksFolder);
                logger.LogInformation($"MSBuildSDKsPath environment variable set to {msbuildSdksFolder}");
            }
            else
            {
                logger.LogError("Could not locate MSBuild Sdks path to set MSBuildSDKsPath");
            }

            s_msbuildFolder = msbuildFolder;
            s_isInitialized = true;
        }

        private static void EnsureInitialized()
        {
            if (!s_isInitialized)
            {
                throw new InvalidOperationException("MSBuildEnvironment is not initialized");
            }
        }

        private static string FindMSBuildFolderFromSolution()
        {
            // Try to locate the appropriate build-time msbuild folder by searching for
            // the OmniSharp solution relative to the current folder.

            var current = Directory.GetCurrentDirectory();
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
            var folderName = ".msbuild-netcoreapp1.0";
#endif

            var result = Path.Combine(current, folderName);

            return Directory.Exists(result)
                ? result
                : null;
        }
    }
}
