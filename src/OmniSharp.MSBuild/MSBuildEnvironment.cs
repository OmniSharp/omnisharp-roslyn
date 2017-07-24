using System;
using System.IO;
using Microsoft.Extensions.Logging;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild
{
    public enum MSBuildEnvironmentKind
    {
        VisualStudio,
        Mono,
        StandAlone
    }

    public static class MSBuildEnvironment
    {
        public const string MSBuildExePathName = "MSBUILD_EXE_PATH";
        public const string MSBuildExtensionsPathName = "MSBuildExtensionsPath";

        private static bool s_isInitialized;
        private static MSBuildEnvironmentKind s_kind;

        private static string s_msbuildExePath;
        private static string s_msbuildExtensionsPath;

        public static bool IsInitialized => s_isInitialized;

        public static MSBuildEnvironmentKind Kind
        {
            get
            {
                ThrowIfNotInitialized();
                return s_kind;
            }
        }

        public static string MSBuildExePath
        {
            get
            {
                ThrowIfNotInitialized();
                return s_msbuildExePath;
            }
        }

        public static string MSBuildExtensionsPath
        {
            get
            {
                ThrowIfNotInitialized();
                return s_msbuildExtensionsPath;
            }
        }

        private static void ThrowIfNotInitialized()
        {
            if (!s_isInitialized)
            {
                throw new InvalidOperationException("MSBuild environment has not been initialized.");
            }
        }

        public static void Initialize(ILogger logger)
        {
            if (s_isInitialized)
            {
                throw new InvalidOperationException("MSBuild environment is already initialized.");
            }

            // If MSBuild can locate VS 2017 and set up a build environment, we don't need to do anything.
            // MSBuild will take care of itself.
            if (MSBuildHelpers.CanInitializeVisualStudioBuildEnvironment())
            {
                logger.LogInformation("MSBuild will use local Visual Studio installation.");
                s_kind = MSBuildEnvironmentKind.VisualStudio;
                s_isInitialized = true;
            }
#if NET46
            else if (TryWithMonoMSBuild(logger, out var monoMSBuildPath, out var monoMSBuildExtensionsPath))
            {
                logger.LogInformation("MSBuild will use Mono installation.");
                s_kind = MSBuildEnvironmentKind.Mono;
                s_msbuildExePath = monoMSBuildPath;
                s_msbuildExtensionsPath = monoMSBuildExtensionsPath;
                s_isInitialized = true;
            }
#endif
            else if (TryWithLocalMSBuild(logger, out var localMSBuildPath, out var localMSBuildExtensionsPath))
            {
                logger.LogInformation("MSBuild will use local OmniSharp installation.");
                s_kind = MSBuildEnvironmentKind.StandAlone;
                s_msbuildExePath = localMSBuildPath;
                s_msbuildExtensionsPath = localMSBuildExtensionsPath;
                s_isInitialized = true;
            }

            if (!s_isInitialized)
            {
                logger.LogError("MSBuild environment could not be initialized.");
            }
        }

        private static void SetEnvionmentVariables(ILogger logger, string msbuildExePath, string msbuildExtensionsPath)
        {
            if (msbuildExePath != null)
            {
                Environment.SetEnvironmentVariable(MSBuildExePathName, msbuildExePath);
                logger.LogInformation($"{MSBuildExePathName} environment variable set to {msbuildExePath}");
            }

            if (msbuildExtensionsPath != null)
            {
                Environment.SetEnvironmentVariable(MSBuildExtensionsPathName, msbuildExtensionsPath);
                logger.LogInformation($"{MSBuildExtensionsPathName} environment variable set to {msbuildExtensionsPath}");
            }
        }

        private static bool TryWithMonoMSBuild(ILogger logger, out string msbuildExePath, out string msbuildExtensionsPath)
        {
            msbuildExePath = null;
            msbuildExtensionsPath = null;

            if (PlatformHelper.IsWindows)
            {
                return false;
            }

            var monoMSBuildDirPath = PlatformHelper.GetMonoMSBuildDirPath();
            if (monoMSBuildDirPath == null)
            {
                // No Mono msbuild folder? If so, use standalone mode.
                return false;
            }

            var monoMSBuildBinDirPath = Path.Combine(monoMSBuildDirPath, "15.0", "bin");

            msbuildExePath = Path.Combine(monoMSBuildBinDirPath, "MSBuild.dll");
            if (!File.Exists(msbuildExePath))
            {
                // Could not locate Mono MSBuild.
                return false;
            }

            // Note: we intentionally don't set msbuildExtensionsPath. This will be set through MSBuild targets.

            SetEnvionmentVariables(logger, msbuildExePath, msbuildExtensionsPath);

            return true;
        }

        private static bool TryWithLocalMSBuild(ILogger logger, out string msbuildExePath, out string msbuildExtensionsPath)
        {
            msbuildExePath = null;
            msbuildExtensionsPath = null;

            var msbuildDirectory = FindMSBuildDirectory(logger);
            if (msbuildDirectory == null)
            {
                logger.LogError("Could not locate MSBuild directory.");
                return false;
            }

            // Set the MSBUILD_EXE_PATH environment variable to the location of MSBuild.exe or MSBuild.dll.
            msbuildExePath = FindMSBuildExe(msbuildDirectory);
            if (msbuildExePath == null)
            {
                logger.LogError("Could not locate MSBuild executable");
                return false;
            }

            // Set the MSBuildExtensionsPath environment variable to the local msbuild directory.
            msbuildExtensionsPath = msbuildDirectory;

            SetEnvionmentVariables(logger, msbuildExePath, msbuildExtensionsPath);

            return true;
        }

        private static string FindMSBuildDirectory(ILogger logger)
        {
            // If OmniSharp is running normally, MSBuild is located in an 'msbuild' folder beneath OmniSharp.exe.
            var msbuildDirectory = Path.Combine(AppContext.BaseDirectory, "msbuild");

            if (!Directory.Exists(msbuildDirectory))
            {
                // If the 'msbuild' folder does not exist beneath OmniSharp.exe, this is likely a development scenario,
                // such as debugging or running unit tests. In that case, we use one of the .msbuild-* folders at the
                // solution level.
                msbuildDirectory = FindLocalMSBuildFromSolution(logger);
            }

            return msbuildDirectory;
        }

        private static string FindLocalMSBuildFromSolution(ILogger logger)
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

        private static string FindMSBuildExe(string directory)
        {
            // We look for either MSBuild.exe or MSBuild.dll.
            var msbuildExePath = Path.Combine(directory, "MSBuild.exe");
            if (File.Exists(msbuildExePath))
            {
                return msbuildExePath;
            }

            msbuildExePath = Path.Combine(directory, "MSBuild.dll");
            if (File.Exists(msbuildExePath))
            {
                return msbuildExePath;
            }

            return null;
        }
    }
}
