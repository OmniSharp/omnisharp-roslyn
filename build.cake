#load "scripts/common.cake"
#load "scripts/runhelpers.cake"
#load "scripts/archiving.cake"
#load "scripts/artifacts.cake"
#load "scripts/platform.cake"
#load "scripts/validation.cake"

using System.ComponentModel;
using System.Net;

// Arguments
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");
var installFolder = Argument("install-path",
    CombinePaths(Environment.GetEnvironmentVariable(Platform.Current.IsWindows ? "USERPROFILE" : "HOME"), ".omnisharp"));
var publishAll = HasArgument("publish-all");
var useGlobalDotNetSdk = HasArgument("use-global-dotnet-sdk");

Log.Context = Context;

var env = new BuildEnvironment(useGlobalDotNetSdk, Context);
var buildPlan = BuildPlan.Load(env);

Information("");
Information("Current platform: {0}", Platform.Current);
Information("");

/// <summary>
/// Checks the current platform to determine whether we allow legacy tests to run.
/// </summary>
bool AllowLegacyTests()
{
    var platform = Platform.Current;

    if (platform.IsMacOS && TravisCI.IsRunningOnTravisCI) return false;

    if (platform.IsWindows)
    {
        return true;
    }

    // On macOS, only run legacy tests on Sierra or lower
    if (platform.IsMacOS)
    {
        return platform.Version.Major == 10
            && platform.Version.Minor <= 12;
    }

    if (platform.IsLinux)
    {
        var version = platform.Version?.ToString();

        // Taken from https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/dotnet-install.sh
        switch (platform.DistroName)
        {
            case "alpine":   return version == "3.4.3";
            case "centos":   return version == "7.0";
            case "debian":   return version == "8.0";
            case "fedora":   return version == "23" || version == "24";
            case "opensuse": return version == "13.2" || version == "42.1";
            case "rhel":     return version == "7.0";
            case "ubuntu":   return version == "14.4" || version == "16.4" || version == "16.10";
        }
    }

    return false;
}

if (!AllowLegacyTests())
{
    Information("Legacy project.json tests will not be run");
}

/// <summary>
///  Clean artifacts.
/// </summary>
Task("Cleanup")
    .Does(() =>
{
    if (DirectoryHelper.Exists(env.Folders.Artifacts))
    {
        DirectoryHelper.Delete(env.Folders.Artifacts, recursive: true);
    }

    DirectoryHelper.Create(env.Folders.Artifacts);
    DirectoryHelper.Create(env.Folders.ArtifactsLogs);
    DirectoryHelper.Create(env.Folders.ArtifactsPackage);
    DirectoryHelper.Create(env.Folders.ArtifactsScripts);
});

Task("GitVersion")
    .WithCriteria(!BuildSystem.IsLocalBuild)
    .WithCriteria(!TFBuild.IsRunningOnTFS)
    .Does(() => {
        GitVersion(new GitVersionSettings{
            OutputType = GitVersionOutput.BuildServer
        });
    });

/// <summary>
///  Pre-build setup tasks.
/// </summary>
Task("Setup")
    .IsDependentOn("GitVersion")
    .IsDependentOn("ValidateMono")
    .IsDependentOn("InstallDotNetCoreSdk")
    .IsDependentOn("InstallMonoAssets")
    .IsDependentOn("CreateMSBuildFolder");

void InstallDotNetSdk(BuildEnvironment env, BuildPlan plan, string version, string installFolder, bool sharedRuntime = false, bool noPath = false)
{
    if (!DirectoryHelper.Exists(installFolder))
    {
        DirectoryHelper.Create(installFolder);
    }

    var scriptFileName = $"dotnet-install.{env.ShellScriptFileExtension}";
    var scriptFilePath = CombinePaths(installFolder, scriptFileName);
    var url = $"{plan.DotNetInstallScriptURL}/{scriptFileName}";

    using (var client = new WebClient())
    {
        client.DownloadFile(url, scriptFilePath);
    }

    if (!Platform.Current.IsWindows)
    {
        Run("chmod", $"+x '{scriptFilePath}'");
    }

    var argList = new List<string>();

    argList.Add("-Channel");
    argList.Add(plan.DotNetChannel);

    if (!string.IsNullOrEmpty(version))
    {
        argList.Add("-Version");
        argList.Add(version);
    }

    argList.Add("-InstallDir");
    argList.Add(installFolder);

    if (sharedRuntime)
    {
        argList.Add("-SharedRuntime");
    }

    if (noPath)
    {
        argList.Add("-NoPath");
    }

    Run(env.ShellCommand, $"{env.ShellArgument} {scriptFilePath} {string.Join(" ", argList)}").ExceptionOnError($"Failed to Install .NET Core SDK {version}");
}

Task("InstallDotNetCoreSdk")
    .Does(() =>
{
    if (!useGlobalDotNetSdk)
    {
        InstallDotNetSdk(env, buildPlan,
            version: buildPlan.DotNetVersion,
            installFolder: env.Folders.DotNetSdk);

        foreach (var runtimeVersion in buildPlan.DotNetSharedRuntimeVersions)
        {
            InstallDotNetSdk(env, buildPlan,
                version: runtimeVersion,
                installFolder: env.Folders.DotNetSdk,
                sharedRuntime: true);
        }

        // Add non-legacy .NET SDK to PATH
        var oldPath = Environment.GetEnvironmentVariable("PATH");
        var newPath = env.Folders.DotNetSdk + (string.IsNullOrEmpty(oldPath) ? "" : System.IO.Path.PathSeparator + oldPath);
        Environment.SetEnvironmentVariable("PATH", newPath);
        Information("PATH: {0}", Environment.GetEnvironmentVariable("PATH"));
    }

    if (AllowLegacyTests())
    {
        // Install legacy .NET Core SDK (used to 'dotnet restore' project.json test projects)
        InstallDotNetSdk(env, buildPlan,
            version: buildPlan.LegacyDotNetVersion,
            installFolder: env.Folders.LegacyDotNetSdk,
            noPath: true);
    }

    // Disable the first time run experience. We don't need to expand all of .NET Core just to build OmniSharp.
    Environment.SetEnvironmentVariable("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "true");

    Run(env.DotNetCommand, "--info");
});

Task("ValidateMono")
    .WithCriteria(() => !Platform.Current.IsWindows)
    .Does(() =>
{
    ValidateMonoVersion(buildPlan);
});

Task("InstallMonoAssets")
    .WithCriteria(() => !Platform.Current.IsWindows)
    .Does(() =>
{
    Information("Acquiring Mono runtimes and framework...");

    DownloadFileAndUnzip($"{buildPlan.DownloadURL}/{buildPlan.MonoRuntimeMacOS}", env.Folders.MonoRuntimeMacOS);
    DownloadFileAndUnzip($"{buildPlan.DownloadURL}/{buildPlan.MonoRuntimeLinux32}", env.Folders.MonoRuntimeLinux32);
    DownloadFileAndUnzip($"{buildPlan.DownloadURL}/{buildPlan.MonoRuntimeLinux64}", env.Folders.MonoRuntimeLinux64);
    DownloadFileAndUnzip($"{buildPlan.DownloadURL}/{buildPlan.MonoMSBuildRuntime}", env.Folders.MonoMSBuildRuntime);
    DownloadFileAndUnzip($"{buildPlan.DownloadURL}/{buildPlan.MonoMSBuildLib}", env.Folders.MonoMSBuildLib);

    var monoInstallFolder = env.CurrentMonoRuntime.InstallFolder;
    var monoRuntimeFile = env.CurrentMonoRuntime.RuntimeFile;

    DirectoryHelper.ForceCreate(env.Folders.Mono);
    DirectoryHelper.Copy(monoInstallFolder, env.Folders.Mono);

    var frameworkFolder = CombinePaths(env.Folders.Mono, "framework");
    DirectoryHelper.ForceCreate(frameworkFolder);

    Run("chmod", $"+x '{CombinePaths(env.Folders.Mono, "bin", monoRuntimeFile)}'");
    Run("chmod", $"+x '{CombinePaths(env.Folders.Mono, "run")}'");
});

void CopyDotNetHostResolver(BuildEnvironment env, string os, string arch, string hostFileName, string targetFolderBase, bool copyToArchSpecificFolder)
{
    var source = CombinePaths(
        env.Folders.Tools,
        $"runtime.{os}-{arch}.Microsoft.NETCore.DotNetHostResolver",
        "runtimes",
        $"{os}-{arch}",
        "native",
        hostFileName);

    var targetFolder = targetFolderBase;

    if (copyToArchSpecificFolder)
    {
        targetFolder = CombinePaths(targetFolderBase, arch);
        DirectoryHelper.ForceCreate(targetFolder);
    }

    FileHelper.Copy(source, CombinePaths(targetFolder, hostFileName));
}

/// <summary>
/// Create '.msbuild' folder and copy content to it.
/// </summary>
Task("CreateMSBuildFolder")
    .IsDependentOn("InstallMonoAssets")
    .Does(() =>
{
    DirectoryHelper.ForceCreate(env.Folders.MSBuild);

    var msbuildCurrentTargetFolder = CombinePaths(env.Folders.MSBuild, "Current");
    var msbuildCurrentBinTargetFolder = CombinePaths(msbuildCurrentTargetFolder, "Bin");

    var msbuildLibraries = new []
    {
        "Microsoft.Build",
        "Microsoft.Build.Framework",
        "Microsoft.Build.Tasks.Core",
        "Microsoft.Build.Utilities.Core"
    };

    string sdkResolverTFM;

    if (Platform.Current.IsWindows)
    {
        Information("Copying MSBuild runtime...");

        var msbuildSourceFolder = CombinePaths(env.Folders.Tools, "Microsoft.Build.Runtime", "contentFiles", "any", "net472");
        DirectoryHelper.Copy(msbuildSourceFolder, msbuildCurrentBinTargetFolder, copySubDirectories: false);

        var msbuild15SourceFolder = CombinePaths(msbuildSourceFolder, "Current");
        DirectoryHelper.Copy(msbuild15SourceFolder, msbuildCurrentTargetFolder);

        Information("Copying MSBuild libraries...");

        foreach (var library in msbuildLibraries)
        {
            var libraryFileName = library + ".dll";
            var librarySourcePath = CombinePaths(env.Folders.Tools, library, "lib", "net472", libraryFileName);
            var libraryTargetPath = CombinePaths(msbuildCurrentBinTargetFolder, libraryFileName);
            FileHelper.Copy(librarySourcePath, libraryTargetPath);
        }

        sdkResolverTFM = "net472";
    }
    else
    {
        Information("Copying Mono MSBuild runtime...");

        var msbuildSourceFolder = env.Folders.MonoMSBuildRuntime;
        DirectoryHelper.Copy(msbuildSourceFolder, msbuildCurrentBinTargetFolder, copySubDirectories: false);

        var msbuild15SourceFolder = CombinePaths(msbuildSourceFolder, "15.0");
        DirectoryHelper.Copy(msbuild15SourceFolder, msbuildCurrentTargetFolder);

        Information("Copying MSBuild libraries...");

        foreach (var library in msbuildLibraries)
        {
            var libraryFileName = library + ".dll";
            var librarySourcePath = CombinePaths(env.Folders.MonoMSBuildLib, libraryFileName);
            var libraryTargetPath = CombinePaths(msbuildCurrentBinTargetFolder, libraryFileName);
            FileHelper.Copy(librarySourcePath, libraryTargetPath);
        }

        sdkResolverTFM = "netstandard2.0";
    }

    // Copy MSBuild SDK Resolver and DotNetHostResolver
    Information("Copying MSBuild SDK resolver...");
    var sdkResolverSourceFolder = CombinePaths(env.Folders.Tools, "Microsoft.DotNet.MSBuildSdkResolver", "lib", sdkResolverTFM);
    var sdkResolverTargetFolder = CombinePaths(msbuildCurrentBinTargetFolder, "SdkResolvers", "Microsoft.DotNet.MSBuildSdkResolver");
    DirectoryHelper.ForceCreate(sdkResolverTargetFolder);
    FileHelper.Copy(
        source: CombinePaths(sdkResolverSourceFolder, "Microsoft.DotNet.MSBuildSdkResolver.dll"),
        destination: CombinePaths(sdkResolverTargetFolder, "Microsoft.DotNet.MSBuildSdkResolver.dll"));

    if (Platform.Current.IsWindows)
    {
        CopyDotNetHostResolver(env, "win", "x86", "hostfxr.dll", sdkResolverTargetFolder, copyToArchSpecificFolder: true);
        CopyDotNetHostResolver(env, "win", "x64", "hostfxr.dll", sdkResolverTargetFolder, copyToArchSpecificFolder: true);
    }
    else if (Platform.Current.IsMacOS)
    {
        CopyDotNetHostResolver(env, "osx", "x64", "libhostfxr.dylib", sdkResolverTargetFolder, copyToArchSpecificFolder: false);
    }
    else if (Platform.Current.IsLinux)
    {
        CopyDotNetHostResolver(env, "linux", "x64", "libhostfxr.so", sdkResolverTargetFolder, copyToArchSpecificFolder: false);
    }

    // Copy content of NuGet.Build.Tasks
    var nugetBuildTasksFolder = CombinePaths(env.Folders.Tools, "NuGet.Build.Tasks");
    var nugetBuildTasksBinariesFolder = CombinePaths(nugetBuildTasksFolder, "lib", "net472");
    var nugetBuildTasksTargetsFolder = CombinePaths(nugetBuildTasksFolder, "runtimes", "any", "native");

    FileHelper.Copy(
        source: CombinePaths(nugetBuildTasksBinariesFolder, "NuGet.Build.Tasks.dll"),
        destination: CombinePaths(msbuildCurrentBinTargetFolder, "NuGet.Build.Tasks.dll"));

    FileHelper.Copy(
        source: CombinePaths(nugetBuildTasksTargetsFolder, "NuGet.targets"),
        destination: CombinePaths(msbuildCurrentBinTargetFolder, "NuGet.targets"));

    // Copy dependencies of NuGet.Build.Tasks
    var nugetPackages = new []
    {
        "NuGet.Commands",
        "NuGet.Common",
        "NuGet.Configuration",
        "NuGet.Frameworks",
        "NuGet.ProjectModel",
        "NuGet.Protocol",   
        "NuGet.Versioning"
    };

    foreach (var nugetPackage in nugetPackages)
    {
        var binaryName = nugetPackage + ".dll";

        FileHelper.Copy(
            source: CombinePaths(env.Folders.Tools, nugetPackage, "lib", "net472", binaryName),
            destination: CombinePaths(msbuildCurrentBinTargetFolder, binaryName));
    }

    // Copy content of Microsoft.Net.Compilers
    Information("Copying Microsoft.Net.Compilers...");
    var compilersSourceFolder = CombinePaths(env.Folders.Tools, "Microsoft.Net.Compilers", "tools");
    var compilersTargetFolder = CombinePaths(msbuildCurrentBinTargetFolder, "Roslyn");

    DirectoryHelper.Copy(compilersSourceFolder, compilersTargetFolder);

    // Delete unnecessary files
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "Microsoft.CodeAnalysis.VisualBasic.dll"));
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "Microsoft.VisualBasic.Core.targets"));
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "VBCSCompiler.exe"));
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "VBCSCompiler.exe.config"));
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "vbc.exe"));
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "vbc.exe.config"));
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "vbc.rsp"));

     FileHelper.Copy(
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.core", "lib", "net45", "SQLitePCLRaw.core.dll"),
        destination: CombinePaths(msbuildCurrentBinTargetFolder, "SQLitePCLRaw.core.dll"),
        overwrite: true);

    FileHelper.Copy(
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.provider.e_sqlite3.net45", "lib", "net45", "SQLitePCLRaw.provider.e_sqlite3.dll"),
        destination: CombinePaths(msbuildCurrentBinTargetFolder, "SQLitePCLRaw.provider.e_sqlite3.dll"),
        overwrite: true);

    FileHelper.Copy(
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.bundle_green", "lib", "net45", "SQLitePCLRaw.batteries_v2.dll"),
        destination: CombinePaths(msbuildCurrentBinTargetFolder, "SQLitePCLRaw.batteries_v2.dll"),
        overwrite: true);

    FileHelper.Copy(
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.bundle_green", "lib", "net45", "SQLitePCLRaw.batteries_green.dll"),
        destination: CombinePaths(msbuildCurrentBinTargetFolder, "SQLitePCLRaw.batteries_green.dll"),
        overwrite: true);

    var msbuild15TargetFolder = CombinePaths(env.Folders.MSBuild, "15.0");
    if (!Platform.Current.IsWindows)
    {
        DirectoryHelper.Copy(msbuildCurrentTargetFolder, msbuild15TargetFolder);
    }
});

/// <summary>
///  Prepare test assets.
/// </summary>
Task("PrepareTestAssets")
    .IsDependentOn("Setup");

Task("PrepareTestAssets:CommonTestAssets")
    .IsDependeeOf("PrepareTestAssets")
    .DoesForEach(buildPlan.TestAssets, (project) =>
    {
        Information("Restoring and building: {0}...", project);

        var folder = CombinePaths(env.Folders.TestAssets, "test-projects", project);

        DotNetCoreBuild(folder, new DotNetCoreBuildSettings()
        {
            ToolPath = env.DotNetCommand,
            WorkingDirectory = folder,
            Verbosity = DotNetCoreVerbosity.Minimal
        });
    });

Task("PrepareTestAssets:RestoreOnlyTestAssets")
    .IsDependeeOf("PrepareTestAssets")
    .DoesForEach(buildPlan.RestoreOnlyTestAssets, (project) =>
    {
        Information("Restoring: {0}...", project);

        var folder = CombinePaths(env.Folders.TestAssets, "test-projects", project);

        DotNetCoreRestore(new DotNetCoreRestoreSettings()
        {
            ToolPath = env.DotNetCommand,
            WorkingDirectory = folder,
            Verbosity = DotNetCoreVerbosity.Minimal
        });
    });

Task("PrepareTestAssets:WindowsOnlyTestAssets")
    .WithCriteria(Platform.Current.IsWindows)
    .IsDependeeOf("PrepareTestAssets")
    .DoesForEach(buildPlan.WindowsOnlyTestAssets, (project) =>
    {
        Information("Restoring and building: {0}...", project);

        var folder = CombinePaths(env.Folders.TestAssets, "test-projects", project);

        DotNetCoreBuild(folder, new DotNetCoreBuildSettings()
        {
            ToolPath = env.DotNetCommand,
            WorkingDirectory = folder,
            Verbosity = DotNetCoreVerbosity.Minimal
        });
    });

Task("PrepareTestAssets:LegacyTestAssets")
    .WithCriteria(() => AllowLegacyTests())
    .IsDependeeOf("PrepareTestAssets")
    .DoesForEach(buildPlan.LegacyTestAssets, (project) =>
    {
        var platform = Platform.Current;
        if (platform.IsMacOS && platform.Version.Major == 10 && platform.Version.Minor == 12)
        {
            // Trick to allow older .NET Core SDK to run on Sierra.
            Environment.SetEnvironmentVariable("DOTNET_RUNTIME_ID", "osx.10.11-x64");
        }

        Information("Restoring and building project.json: {0}...", project);

        var folder = CombinePaths(env.Folders.TestAssets, "legacy-test-projects", project);

        DotNetCoreRestore(new DotNetCoreRestoreSettings()
        {
            ToolPath = env.LegacyDotNetCommand,
            WorkingDirectory = folder,
            Verbosity = DotNetCoreVerbosity.Minimal
        });

        DotNetCoreBuild(folder, new DotNetCoreBuildSettings()
        {
            ToolPath = env.LegacyDotNetCommand,
            WorkingDirectory = folder
        });
    });

Task("PrepareTestAssets:CakeTestAssets")
    .IsDependeeOf("PrepareTestAssets")
    .DoesForEach(buildPlan.CakeTestAssets, (project) =>
    {
        Information("Restoring: {0}...", project);

        var toolsFolder = CombinePaths(env.Folders.TestAssets, "test-projects", project, "tools");
        var packagesConfig = CombinePaths(toolsFolder, "packages.config");

        NuGetInstallFromConfig(packagesConfig, new NuGetInstallSettings {
            OutputDirectory = toolsFolder,
            Prerelease = true,
            Verbosity = NuGetVerbosity.Quiet,
            Source = new[] {
                "https://api.nuget.org/v3/index.json"
            }
        });
    });

void BuildWithDotNetCli(BuildEnvironment env, string configuration)
{
    Information("Building OmniSharp.sln with .NET Core CLI...");

    var logFileNameBase = CombinePaths(env.Folders.ArtifactsLogs, "OmniSharp-build");
    var projectImports = Context.Log.Verbosity == Verbosity.Verbose || Context.Log.Verbosity == Verbosity.Diagnostic
        ? MSBuildBinaryLogImports.Embed
        : MSBuildBinaryLogImports.None;

    var settings = new DotNetCoreMSBuildSettings
    {
        WorkingDirectory = env.WorkingDirectory,

        ArgumentCustomization = args =>
            args.Append("/restore")
                .Append($"/bl:{logFileNameBase}.binlog;ProjectImports={projectImports}")
                .Append($"/v:{Verbosity.Minimal.GetMSBuildVerbosityName()}")
    };

    settings.AddFileLogger(
        new MSBuildFileLoggerSettings {
            AppendToLogFile = false,
            LogFile = logFileNameBase + ".log",
            ShowTimestamp = true,

            // Bug in cake with this
            // Verbosity = Verbosity.Diagnostic,
        }
    );

    settings
        .SetConfiguration(configuration)
        .WithProperty("PackageVersion", env.VersionInfo.NuGetVersion)
        .WithProperty("AssemblyVersion", env.VersionInfo.AssemblySemVer)
        .WithProperty("FileVersion", env.VersionInfo.AssemblySemVer)
        .WithProperty("InformationalVersion", env.VersionInfo.InformationalVersion);

    DotNetCoreMSBuild("OmniSharp.sln", settings);
}

void BuildWithMSBuild(BuildEnvironment env, string configuration)
{
    Information("Building OmniSharp.sln with MSBuild...");

    var logFileNameBase = CombinePaths(env.Folders.ArtifactsLogs, "OmniSharp-build");
    var projectImports = Context.Log.Verbosity == Verbosity.Verbose || Context.Log.Verbosity == Verbosity.Diagnostic
        ? MSBuildBinaryLogImports.Embed
        : MSBuildBinaryLogImports.None;

    MSBuild("OmniSharp.sln", settings =>
    {
        settings.WorkingDirectory = env.WorkingDirectory;

        settings.ArgumentCustomization = args =>
            args.Append("/restore");

        settings.AddFileLogger(
            new MSBuildFileLogger {
                AppendToLogFile = false,
                LogFile = logFileNameBase + ".log",
                ShowTimestamp = true,
                Verbosity = Verbosity.Diagnostic,
                PerformanceSummaryEnabled = true,
                SummaryDisabled = false
            }
        );

        settings.BinaryLogger = new MSBuildBinaryLogSettings {
            Enabled = true,
            FileName = logFileNameBase + ".binlog",
            Imports = projectImports
        };

        settings
            .SetConfiguration(configuration)
            .SetVerbosity(Verbosity.Minimal)
            .WithProperty("PackageVersion", env.VersionInfo.NuGetVersion)
            .WithProperty("AssemblyVersion", env.VersionInfo.AssemblySemVer)
            .WithProperty("FileVersion", env.VersionInfo.AssemblySemVer)
            .WithProperty("InformationalVersion", env.VersionInfo.InformationalVersion);
    });
}

Task("Build")
    .IsDependentOn("Setup")
    .Does(() =>
{
    try
    {
        if (Platform.Current.IsWindows)
        {
            BuildWithDotNetCli(env, configuration);
        }
        else
        {
            BuildWithMSBuild(env, configuration);
        }
    }
    catch
    {
        Error($"Build failed.");
        throw;
    }
});

/// <summary>
///  Run all tests.
/// </summary>
Task("Test")
    .IsDependentOn("Setup")
    .IsDependentOn("Build")
    .IsDependentOn("PrepareTestAssets")
    .Does(() =>
{
    if (!AllowLegacyTests())
    {
        Environment.SetEnvironmentVariable("OMNISHARP_NO_LEGACY_TESTS", "True");
    }

    try
    {
        foreach (var testProject in buildPlan.TestProjects)
        {
            PrintBlankLine();
            var instanceFolder = CombinePaths(env.Folders.Bin, configuration, testProject, "net472");

            // Copy xunit executable to test folder to solve path errors
            var xunitToolsFolder = CombinePaths(env.Folders.Tools, "xunit.runner.console", "tools", "net452");
            var xunitInstancePath = CombinePaths(instanceFolder, "xunit.console.exe");
            FileHelper.Copy(CombinePaths(xunitToolsFolder, "xunit.console.exe"), xunitInstancePath, overwrite: true);
            FileHelper.Copy(CombinePaths(xunitToolsFolder, "xunit.runner.utility.net452.dll"), CombinePaths(instanceFolder, "xunit.runner.utility.net452.dll"), overwrite: true);
            var targetPath = CombinePaths(instanceFolder, $"{testProject}.dll");
            var logFile = CombinePaths(env.Folders.ArtifactsLogs, $"{testProject}-desktop-result.xml");
            var arguments = $"\"{targetPath}\" -noshadow -parallel none -xml \"{logFile}\" -notrait category=failing";

            if (Platform.Current.IsWindows)
            {
                Run(xunitInstancePath, arguments, instanceFolder)
                    .ExceptionOnError($"Test {testProject} failed for net472");
            }
            else
            {
                // Copy the Mono-built Microsoft.Build.* binaries to the test folder.
                // This is necessary to work around a Mono bug that is exasperated by xUnit.
                DirectoryHelper.Copy($"{env.Folders.MonoMSBuildLib}", instanceFolder);

                var runScript = CombinePaths(env.Folders.Mono, "run");

                // By default, the run script launches OmniSharp. To launch our Mono runtime
                // with xUnit rather than OmniSharp, we pass '--no-omnisharp'
                Run(runScript, $"--no-omnisharp \"{xunitInstancePath}\" {arguments}", instanceFolder)
                    .ExceptionOnError($"Test {testProject} failed for net472");
            }
        }
    }
    finally
    {
        Environment.SetEnvironmentVariable("OMNISHARP_NO_LEGACY_TESTS", null);
    }
});

void CopyMonoBuild(BuildEnvironment env, string sourceFolder, string outputFolder)
{
    DirectoryHelper.Copy(sourceFolder, outputFolder, copySubDirectories: false);

    var msbuildFolder = CombinePaths(outputFolder, ".msbuild");

    // Copy MSBuild runtime and libraries
    DirectoryHelper.Copy($"{env.Folders.MSBuild}", msbuildFolder);

    var msbuildBinFolder = CombinePaths(msbuildFolder, "bin", "Current");
    EnsureDirectoryExists(msbuildBinFolder);

    // Copy dependencies of Mono build
    FileHelper.Copy(
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.core", "lib", "net45", "SQLitePCLRaw.core.dll"),
        destination: CombinePaths(msbuildBinFolder, "SQLitePCLRaw.core.dll"),
        overwrite: true);

    FileHelper.Copy(
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.provider.e_sqlite3.net45", "lib", "net45", "SQLitePCLRaw.provider.e_sqlite3.dll"),
        destination: CombinePaths(msbuildBinFolder, "SQLitePCLRaw.provider.e_sqlite3.dll"),
        overwrite: true);

    FileHelper.Copy(
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.bundle_green", "lib", "net45", "SQLitePCLRaw.batteries_v2.dll"),
        destination: CombinePaths(msbuildBinFolder, "SQLitePCLRaw.batteries_v2.dll"),
        overwrite: true);

    FileHelper.Copy(
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.bundle_green", "lib", "net45", "SQLitePCLRaw.batteries_green.dll"),
        destination: CombinePaths(msbuildBinFolder, "SQLitePCLRaw.batteries_green.dll"),
        overwrite: true);
}

void CopyExtraDependencies(BuildEnvironment env, string outputFolder)
{
    // copy license
    FileHelper.Copy(CombinePaths(env.WorkingDirectory, "license.md"), CombinePaths(outputFolder, "license.md"), overwrite: true);
}

string PublishMonoBuild(string project, BuildEnvironment env, BuildPlan plan, string configuration)
{
    Information($"Publishing Mono build for {project}...");

    var outputFolder = CombinePaths(env.Folders.ArtifactsPublish, project, "mono");

    var buildFolder = CombinePaths(env.Folders.Bin, configuration, project, "net472");

    CopyMonoBuild(env, buildFolder, outputFolder);

    CopyExtraDependencies(env, outputFolder);

    Package(project, "mono", outputFolder, env.Folders.ArtifactsPackage, env.Folders.DeploymentPackage);

     // Copy dependencies of Mono build
     FileHelper.Copy(
         source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.core", "lib", "net45", "SQLitePCLRaw.core.dll"),
         destination: CombinePaths(outputFolder, "SQLitePCLRaw.core.dll"),
         overwrite: true);
     FileHelper.Copy(
         source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.provider.e_sqlite3.net45", "lib", "net45", "SQLitePCLRaw.provider.e_sqlite3.dll"),
         destination: CombinePaths(outputFolder, "SQLitePCLRaw.provider.e_sqlite3.dll"),
         overwrite: true);
     FileHelper.Copy(
         source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.bundle_green", "lib", "net45", "SQLitePCLRaw.batteries_v2.dll"),
         destination: CombinePaths(outputFolder, "SQLitePCLRaw.batteries_v2.dll"),
         overwrite: true);
     FileHelper.Copy(
         source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.bundle_green", "lib", "net45", "SQLitePCLRaw.batteries_green.dll"),
         destination: CombinePaths(outputFolder, "SQLitePCLRaw.batteries_green.dll"),
         overwrite: true);

    return outputFolder;
}

string PublishMonoBuildForPlatform(string project, MonoRuntime monoRuntime, BuildEnvironment env, BuildPlan plan)
{
    Information("Publishing platform-specific Mono build: {0}", monoRuntime.PlatformName);

    var outputFolder = CombinePaths(env.Folders.ArtifactsPublish, project, monoRuntime.PlatformName);

    DirectoryHelper.Copy(monoRuntime.InstallFolder, outputFolder);

    Run("chmod", $"+x '{CombinePaths(outputFolder, "bin", monoRuntime.RuntimeFile)}'");
    Run("chmod", $"+x '{CombinePaths(outputFolder, "run")}'");

    var sourceFolder = CombinePaths(env.Folders.ArtifactsPublish, project, "mono");
    var omnisharpFolder = CombinePaths(outputFolder, "omnisharp");

    CopyMonoBuild(env, sourceFolder, omnisharpFolder);

    CopyExtraDependencies(env, outputFolder);

    Package(project, monoRuntime.PlatformName, outputFolder, env.Folders.ArtifactsPackage, env.Folders.DeploymentPackage);

    return outputFolder;
}

Task("PublishMonoBuilds")
    .IsDependentOn("Setup")
    .WithCriteria(() => !Platform.Current.IsWindows)
    .Does(() =>
{
    foreach (var project in buildPlan.HostProjects)
    {
        var outputFolder = PublishMonoBuild(project, env, buildPlan, configuration);

        CreateRunScript(project, outputFolder, env.Folders.ArtifactsScripts);

        if (publishAll)
        {
            foreach (var monoRuntime in env.BuildMonoRuntimes)
            {
                PublishMonoBuildForPlatform(project, monoRuntime, env, buildPlan);
            }
        }
    }
});

string PublishWindowsBuild(string project, BuildEnvironment env, BuildPlan plan, string configuration, string rid)
{
    var projectName = project + ".csproj";
    var projectFileName = CombinePaths(env.Folders.Source, project, projectName);
    var outputFolder = CombinePaths(env.Folders.ArtifactsPublish, project, rid);

    Information("Publishing {0} for {1}...", projectName, rid);

    try
    {
        DotNetCorePublish(projectFileName, new DotNetCorePublishSettings()
        {
            Runtime = rid,
            Configuration = configuration,
            OutputDirectory = outputFolder,
            MSBuildSettings = new DotNetCoreMSBuildSettings()
                .WithProperty("PackageVersion", env.VersionInfo.NuGetVersion)
                .WithProperty("AssemblyVersion", env.VersionInfo.AssemblySemVer)
                .WithProperty("FileVersion", env.VersionInfo.AssemblySemVer)
                .WithProperty("InformationalVersion", env.VersionInfo.InformationalVersion),
            ToolPath = env.DotNetCommand,
            WorkingDirectory = env.WorkingDirectory,
            Verbosity = DotNetCoreVerbosity.Minimal,
        });
    }
    catch
    {
        Error($"Failed to publish {project} for {rid}");
        throw;
    }

    // Copy MSBuild to output
    DirectoryHelper.Copy($"{env.Folders.MSBuild}", CombinePaths(outputFolder, ".msbuild"));

    CopyExtraDependencies(env, outputFolder);

    Package(project, rid, outputFolder, env.Folders.ArtifactsPackage, env.Folders.DeploymentPackage);

    return outputFolder;
}

Task("PublishWindowsBuilds")
    .WithCriteria(() => Platform.Current.IsWindows)
    .IsDependentOn("Setup")
    .Does(() =>
{
    foreach (var project in buildPlan.HostProjects)
    {
        string outputFolder;

        if (publishAll)
        {
            var outputFolder32 = PublishWindowsBuild(project, env, buildPlan, configuration, "win7-x86");
            var outputFolder64 = PublishWindowsBuild(project, env, buildPlan, configuration, "win7-x64");

            outputFolder = Platform.Current.Is32Bit
                ? outputFolder32
                : outputFolder64;
        }
        else if (Platform.Current.Is32Bit)
        {
            outputFolder = PublishWindowsBuild(project, env, buildPlan, configuration, "win7-x86");
        }
        else
        {
            outputFolder = PublishWindowsBuild(project, env, buildPlan, configuration, "win7-x64");
        }

        CreateRunScript(project, outputFolder, env.Folders.ArtifactsScripts);
    }
});

Task("Publish")
    .IsDependentOn("Build")
    .IsDependentOn("PublishMonoBuilds")
    .IsDependentOn("PublishWindowsBuilds");

/// <summary>
///  Execute the run script.
/// </summary>
Task("ExecuteRunScript")
    .WithCriteria(() => !(Platform.Current.IsMacOS && TravisCI.IsRunningOnTravisCI))
    .Does(() =>
{
    // TODO: Pass configuration into run script to ensure that MSBuild output paths are handled correctly.
    // Otherwise, we get "could not resolve output path" messages when building for release.

    foreach (var project in buildPlan.HostProjects)
    {
        var projectFolder = CombinePaths(env.Folders.Source, project);

        var scriptPath = GetScriptPath(env.Folders.ArtifactsScripts, project);
        var didNotExitWithError = Run(env.ShellCommand, $"{env.ShellArgument}  \"{scriptPath}\" -s \"{projectFolder}\"",
                                    new RunOptions(waitForIdle: true))
                                .WasIdle;
        if (!didNotExitWithError)
        {
            throw new Exception($"Failed to run {scriptPath}");
        }
    }
});

/// <summary>
///  Quick build.
/// </summary>
Task("Quick")
    .IsDependentOn("Cleanup")
    .IsDependentOn("Publish");

/// <summary>
///  Clean install path.
/// </summary>
Task("CleanupInstall")
    .Does(() =>
{
    DirectoryHelper.ForceCreate(installFolder);
});

/// <summary>
///  Quick build + install.
/// </summary>
Task("Install")
    .IsDependentOn("Cleanup")
    .IsDependentOn("Publish")
    .IsDependentOn("CleanupInstall")
    .Does(() =>
{
    foreach (var project in buildPlan.HostProjects)
    {
        string platform;
        if (Platform.Current.IsWindows)
        {
            platform = Platform.Current.Is32Bit
                ? "win7-x86"
                : "win7-x64";
        }
        else
        {
            platform = "mono";
        }

        var outputFolder = PathHelper.GetFullPath(CombinePaths(env.Folders.ArtifactsPublish, project, platform));
        var targetFolder = PathHelper.GetFullPath(CombinePaths(installFolder));

        DirectoryHelper.Copy(outputFolder, targetFolder);

        CreateRunScript(project, installFolder, env.Folders.ArtifactsScripts);

        Information($"OmniSharp is installed locally at {installFolder}");
    }
});

/// <summary>
///  Full build and execute script at the end.
/// </summary>
Task("All")
    .IsDependentOn("Cleanup")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Publish")
    .IsDependentOn("ExecuteRunScript");

/// <summary>
///  Default Task aliases to All.
/// </summary>
Task("Default")
    .IsDependentOn("All");

Teardown(context =>
{
    // Ensure that we shutdown all build servers used by the CLI during build.
    Run(env.DotNetCommand, "build-server shutdown");
});

/// <summary>
///  Default to All.
/// </summary>
RunTarget(target);
