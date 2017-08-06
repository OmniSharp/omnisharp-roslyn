using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Utilities;

namespace OmniSharp.MSBuild
{
    public enum MSBuildEnvironmentKind
    {
        Unknown,
        VisualStudio,
        Mono,
        StandAlone
    }

    public static class MSBuildEnvironment
    {
        public const string MSBuildExePathName = "MSBUILD_EXE_PATH";

        private static bool s_isInitialized;
        private static MSBuildEnvironmentKind s_kind;

        private static string s_msbuildExePath;
        private static string s_msbuildExtensionsPath;
        private static string s_targetFrameworkRootPath;
        private static string s_roslynTargetsPath;

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

        public static string TargetFrameworkRootPath
        {
            get
            {
                ThrowIfNotInitialized();
                return s_targetFrameworkRootPath;
            }
        }

        public static string RoslynTargetsPath
        {
            get
            {
                ThrowIfNotInitialized();
                return s_roslynTargetsPath;
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
                s_kind = MSBuildEnvironmentKind.VisualStudio;
                s_isInitialized = true;
            }
            else if (TryWithMonoMSBuild())
            {
                s_kind = MSBuildEnvironmentKind.Mono;
                s_isInitialized = true;
            }
            else if (TryWithLocalMSBuild())
            {
                s_kind = MSBuildEnvironmentKind.StandAlone;
                s_isInitialized = true;
            }
            else
            {
                s_kind = MSBuildEnvironmentKind.Unknown;
                logger.LogError("MSBuild environment could not be initialized.");
                s_isInitialized = false;
            }

            if (!s_isInitialized)
            {
                return;
            }

            var summary = new StringBuilder();
            switch (s_kind)
            {
                case MSBuildEnvironmentKind.VisualStudio:
                    summary.AppendLine("OmniSharp initialized with Visual Studio MSBuild.");
                    break;
                case MSBuildEnvironmentKind.Mono:
                    summary.AppendLine("OmniSharp initialized with Mono MSBuild.");
                    break;
                case MSBuildEnvironmentKind.StandAlone:
                    summary.AppendLine("Omnisharp will use local MSBuild.");
                    break;
            }

            if (s_msbuildExePath != null)
            {
                summary.AppendLine($"    MSBUILD_EXE_PATH: {s_msbuildExePath}");
            }

            if (s_msbuildExtensionsPath != null)
            {
                summary.AppendLine($"    MSBuildExtensionsPath: {s_msbuildExtensionsPath}");
            }

            if (s_targetFrameworkRootPath != null)
            {
                summary.AppendLine($"    TargetFrameworkRootPath: {s_targetFrameworkRootPath}");
            }

            if (s_roslynTargetsPath != null)
            {
                summary.AppendLine($"    RoslynTargetsPath: {s_roslynTargetsPath}");
            }

            logger.LogInformation(summary.ToString());
        }

        private static void SetMSBuildExePath(string msbuildExePath)
        {
            s_msbuildExePath = msbuildExePath;
            Environment.SetEnvironmentVariable(MSBuildExePathName, msbuildExePath);
        }

        private static bool TryWithMonoMSBuild()
        {
            if (!PlatformHelper.IsMono)
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

            var msbuildExePath = Path.Combine(monoMSBuildBinDirPath, "MSBuild.dll");
            if (!File.Exists(msbuildExePath))
            {
                // Could not locate Mono MSBuild.
                return false;
            }

            SetMSBuildExePath(msbuildExePath);

            if (!TrySetMonoVariables())
            {
                s_msbuildExtensionsPath = monoMSBuildDirPath;
            }

            // Use our version of the Roslyn compiler and targets.
            var msbuildDirectory = FindMSBuildDirectory();
            if (msbuildDirectory != null)
            {
                var roslynTargetsPath = Path.Combine(msbuildDirectory, "Roslyn");
                if (Directory.Exists(roslynTargetsPath))
                {
                    s_roslynTargetsPath = roslynTargetsPath;
                }
            }

            return true;
        }

        private static bool TrySetMonoVariables()
        {
            if (!PlatformHelper.IsMono)
            {
                return false;
            }

            var monoMSBuildXBuildDirPath = PlatformHelper.GetMonoXBuildDirPath();
            var monoMSBuildXBuildFrameworksDirPath = PlatformHelper.GetMonoXBuildFrameworksDirPath();

            if (monoMSBuildXBuildDirPath == null &&
                monoMSBuildXBuildFrameworksDirPath == null)
            {
                // No Mono xbuild or xbuild-frameworks folders?.
                return false;
            }

            s_msbuildExtensionsPath = monoMSBuildXBuildDirPath;
            s_targetFrameworkRootPath = monoMSBuildXBuildFrameworksDirPath;

            return true;
        }

        private static bool TryWithLocalMSBuild()
        {
            var msbuildDirectory = FindMSBuildDirectory();
            if (msbuildDirectory == null)
            {
                return false;
            }

            // Set the MSBUILD_EXE_PATH environment variable to the location of MSBuild.exe or MSBuild.dll.
            var msbuildExePath = FindMSBuildExe(msbuildDirectory);
            if (msbuildExePath == null)
            {
                return false;
            }

            SetMSBuildExePath(msbuildExePath);

            if (!TrySetMonoVariables())
            {
                s_msbuildExtensionsPath = msbuildDirectory;
            }

            return true;
        }

        private static string FindMSBuildDirectory()
        {
            // If OmniSharp is running normally, MSBuild is located in an 'msbuild' folder beneath OmniSharp.exe.
            var msbuildDirectory = Path.Combine(AppContext.BaseDirectory, "msbuild");

            if (!Directory.Exists(msbuildDirectory))
            {
                // If the 'msbuild' folder does not exist beneath OmniSharp.exe, this is likely a development scenario,
                // such as debugging or running unit tests. In that case, we use one of the .msbuild-* folders at the
                // solution level.
                msbuildDirectory = FindLocalMSBuildFromSolution();
            }

            return msbuildDirectory;
        }

        private static string FindLocalMSBuildFromSolution()
        {
            // Try to locate the appropriate build-time msbuild folder by searching for
            // the OmniSharp solution relative to the current folder.

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
