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
var configuration = Argument("configuration", "Release");
var testConfiguration = Argument("test-configuration", "Debug");
var installFolder = Argument("install-path",
    CombinePaths(Environment.GetEnvironmentVariable(Platform.Current.IsWindows ? "USERPROFILE" : "HOME"), ".omnisharp"));
var requireArchive = HasArgument("archive");
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
        var version = platform.Version.ToString();

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
    DownloadFileAndUnzip($"{buildPlan.DownloadURL}/{buildPlan.MonoFramework}", env.Folders.MonoFramework);
    DownloadFileAndUnzip($"{buildPlan.DownloadURL}/{buildPlan.MonoMSBuildRuntime}", env.Folders.MonoMSBuildRuntime);
    DownloadFileAndUnzip($"{buildPlan.DownloadURL}/{buildPlan.MonoMSBuildLib}", env.Folders.MonoMSBuildLib);

    var monoInstallFolder = env.CurrentMonoRuntime.InstallFolder;
    var monoRuntimeFile = env.CurrentMonoRuntime.RuntimeFile;

    DirectoryHelper.ForceCreate(env.Folders.Mono);
    DirectoryHelper.Copy(monoInstallFolder, env.Folders.Mono);

    var frameworkFolder = CombinePaths(env.Folders.Mono, "framework");
    DirectoryHelper.ForceCreate(frameworkFolder);
    DirectoryHelper.Copy(env.Folders.MonoFramework, frameworkFolder);

    Run("chmod", $"+x '{CombinePaths(env.Folders.Mono, monoRuntimeFile)}'");
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

    var msbuild15TargetFolder = CombinePaths(env.Folders.MSBuild, "15.0");
    var msbuild15BinTargetFolder = CombinePaths(msbuild15TargetFolder, "Bin");

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

        var msbuildSourceFolder = CombinePaths(env.Folders.Tools, "Microsoft.Build.Runtime", "contentFiles", "any", "net46");
        DirectoryHelper.Copy(msbuildSourceFolder, msbuild15BinTargetFolder, copySubDirectories: false);

        var msbuild15SourceFolder = CombinePaths(msbuildSourceFolder, "15.0");
        DirectoryHelper.Copy(msbuild15SourceFolder, msbuild15TargetFolder);

        Information("Copying MSBuild libraries...");

        foreach (var library in msbuildLibraries)
        {
            var libraryFileName = library + ".dll";
            var librarySourcePath = CombinePaths(env.Folders.Tools, library, "lib", "net46", libraryFileName);
            var libraryTargetPath = CombinePaths(msbuild15BinTargetFolder, libraryFileName);
            FileHelper.Copy(librarySourcePath, libraryTargetPath);
        }

        sdkResolverTFM = "net46";
    }
    else
    {
        Information("Copying Mono MSBuild runtime...");

        var msbuildSourceFolder = env.Folders.MonoMSBuildRuntime;
        DirectoryHelper.Copy(msbuildSourceFolder, msbuild15BinTargetFolder, copySubDirectories: false);

        var msbuild15SourceFolder = CombinePaths(msbuildSourceFolder, "15.0");
        DirectoryHelper.Copy(msbuild15SourceFolder, msbuild15TargetFolder);

        Information("Copying MSBuild libraries...");

        foreach (var library in msbuildLibraries)
        {
            var libraryFileName = library + ".dll";
            var librarySourcePath = CombinePaths(env.Folders.MonoMSBuildLib, libraryFileName);
            var libraryTargetPath = CombinePaths(msbuild15BinTargetFolder, libraryFileName);
            FileHelper.Copy(librarySourcePath, libraryTargetPath);
        }

        sdkResolverTFM = "netstandard1.5";
    }

    // Copy MSBuild SDK Resolver and DotNetHostResolver
    Information("Coping MSBuild SDK resolver...");
    var sdkResolverSourceFolder = CombinePaths(env.Folders.Tools, "Microsoft.DotNet.MSBuildSdkResolver", "lib", sdkResolverTFM);
    var sdkResolverTargetFolder = CombinePaths(msbuild15BinTargetFolder, "SdkResolvers", "Microsoft.DotNet.MSBuildSdkResolver");
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

    // Copy content of Microsoft.Net.Compilers
    Information("Copying Microsoft.Net.Compilers...");
    var compilersSourceFolder = CombinePaths(env.Folders.Tools, "Microsoft.Net.Compilers", "tools");
    var compilersTargetFolder = CombinePaths(msbuild15BinTargetFolder, "Roslyn");

    DirectoryHelper.Copy(compilersSourceFolder, compilersTargetFolder);

    // Delete unnecessary files
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "Microsoft.CodeAnalysis.VisualBasic.dll"));
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "Microsoft.VisualBasic.Core.targets"));
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "VBCSCompiler.exe"));
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "VBCSCompiler.exe.config"));
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "vbc.exe"));
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "vbc.exe.config"));
    FileHelper.Delete(CombinePaths(compilersTargetFolder, "vbc.rsp"));
});

/// <summary>
///  Restore required NuGet packages.
/// </summary>
Task("Restore")
    .IsDependentOn("Setup")
    .Does(() =>
{
    // Restore the projects in OmniSharp.sln
    Information("Restoring packages in OmniSharp.sln...");

    DotNetCoreRestore("OmniSharp.sln", new DotNetCoreRestoreSettings()
    {
        ToolPath = env.DotNetCommand,
        WorkingDirectory = env.WorkingDirectory,
        Verbosity = DotNetCoreVerbosity.Minimal,
    });
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

        DotNetCoreRestore(new DotNetCoreRestoreSettings()
        {
            ToolPath = env.DotNetCommand,
            WorkingDirectory = folder,
            Verbosity = DotNetCoreVerbosity.Minimal,
        });
        DotNetCoreBuild(folder, new DotNetCoreBuildSettings()
        {
            ToolPath = env.DotNetCommand,
            WorkingDirectory = folder,
            Verbosity = DotNetCoreVerbosity.Minimal,
        });
    });

Task("PrepareTestAssets:WindowsTestAssets")
    .WithCriteria(Platform.Current.IsWindows)
    .IsDependeeOf("PrepareTestAssets")
    .DoesForEach(buildPlan.WindowsOnlyTestAssets, (project) =>
    {
        Information("Restoring and building: {0}...", project);

        var folder = CombinePaths(env.Folders.TestAssets, "test-projects", project);

        DotNetCoreRestore(new DotNetCoreRestoreSettings()
        {
            ToolPath = env.DotNetCommand,
            WorkingDirectory = folder,
            Verbosity = DotNetCoreVerbosity.Minimal,
        });
        DotNetCoreBuild(folder, new DotNetCoreBuildSettings()
        {
            ToolPath = env.DotNetCommand,
            WorkingDirectory = folder,
            Verbosity = DotNetCoreVerbosity.Minimal,
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

        var folder = CombinePaths(env.Folders.TestAssets, "test-projects", project);

        DotNetCoreRestore(new DotNetCoreRestoreSettings()
        {
            ToolPath = env.LegacyDotNetCommand,
            WorkingDirectory = folder,
            Verbosity = DotNetCoreVerbosity.Minimal,
        });
        DotNetCoreBuild(folder, new DotNetCoreBuildSettings()
        {
            ToolPath = env.LegacyDotNetCommand,
            WorkingDirectory = folder,
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

void BuildProject(BuildEnvironment env, string projectName, string projectFilePath, string configuration, string outputType = null)
{
    try
    {
        var logFileName = CombinePaths(env.Folders.ArtifactsLogs, $"{projectName}-build");

        Information("Building {0}...", projectName);
        // On Windows, we build exclusively with the .NET CLI.
        // On OSX/Linux, we build with the MSBuild installed with Mono.
        if (Platform.Current.IsWindows)
        {
            var projectImports = Context.Log.Verbosity == Verbosity.Verbose || Context.Log.Verbosity == Verbosity.Diagnostic ? "Embed" : "None";
            var settings = new DotNetCoreMSBuildSettings()
                {
                    WorkingDirectory = env.WorkingDirectory,
                    ArgumentCustomization = a => a
                        .Append($"/bl:{logFileName}.binlog;ProjectImports={projectImports}")
                        .Append($"/v:{Verbosity.Minimal.GetMSBuildVerbosityName()}"),
                    // Bug in cake with this command
                    // Verbosity = DotNetCoreVerbosity.Minimal,
                }
                .SetConfiguration(configuration)
                .AddFileLogger(
                    new MSBuildFileLoggerSettings {
                        AppendToLogFile = false,
                        LogFile = logFileName + ".log",
                        ShowTimestamp = true,
                        //Verbosity = DotNetCoreVerbosity.Diagnostic,
                    }
                )
                .WithProperty("PackageVersion", env.VersionInfo.NuGetVersion)
                .WithProperty("AssemblyVersion", env.VersionInfo.AssemblySemVer)
                .WithProperty("FileVersion", env.VersionInfo.AssemblySemVer)
                .WithProperty("InformationalVersion", env.VersionInfo.InformationalVersion);
            if (!string.IsNullOrWhiteSpace(outputType))
                settings.WithProperty("TestOutputType", outputType);

            DotNetCoreMSBuild(
                projectFilePath,
                settings
            );
        }
        else
        {
            MSBuild(
                projectFilePath,
                c =>
                {
                    c.Verbosity = Verbosity.Minimal;
                    c.Configuration = configuration;
                    c.WorkingDirectory = env.WorkingDirectory;
                    c.AddFileLogger(
                        new MSBuildFileLogger {
                            AppendToLogFile = false,
                            LogFile = logFileName + ".log",
                            ShowTimestamp = true,
                            Verbosity = Verbosity.Diagnostic,
                            PerformanceSummaryEnabled = true,
                            SummaryDisabled = false,
                        }
                    );
                    c.BinaryLogger = new MSBuildBinaryLogSettings {
                        Enabled = true,
                        FileName = logFileName + ".binlog",
                        Imports = Context.Log.Verbosity == Verbosity.Verbose || Context.Log.Verbosity == Verbosity.Diagnostic ? MSBuildBinaryLogImports.Embed : MSBuildBinaryLogImports.None,
                    };
                    c
                        .WithProperty("TestOutputType", outputType)
                        .WithProperty("PackageVersion", env.VersionInfo.NuGetVersion)
                        .WithProperty("AssemblyVersion", env.VersionInfo.AssemblySemVer)
                        .WithProperty("FileVersion", env.VersionInfo.AssemblySemVer)
                        .WithProperty("InformationalVersion", env.VersionInfo.InformationalVersion)
                    ;

                }
            );
        }
    }
    catch
    {
        Error($"Building {projectName} failed.");
        throw;
    }
}

Task("BuildHosts")
    .IsDependentOn("Setup")
    .IsDependentOn("Restore")
    .Does(() =>
{
    foreach (var project in buildPlan.HostProjects)
    {
        var projectName = project + ".csproj";
        var projectFilePath = CombinePaths(env.Folders.Source, project, projectName);

        BuildProject(env, projectName, projectFilePath, configuration);
    }
});

/// <summary>
///  Build Test projects.
/// </summary>
Task("BuildTest")
    .IsDependentOn("Setup")
    .IsDependentOn("Restore")
    .Does(() =>
{
    foreach (var project in buildPlan.HostProjects)
    {
        var projectName = project + ".csproj";
        var projectFilePath = CombinePaths(env.Folders.Source, project, projectName);
        BuildProject(env, projectName, projectFilePath, configuration, "test");
    }

    foreach (var testProject in buildPlan.TestProjects)
    {
        var testProjectName = testProject + ".csproj";
        var testProjectFilePath = CombinePaths(env.Folders.Tests, testProject, testProjectName);

        BuildProject(env, testProjectName, testProjectFilePath, testConfiguration);
    }
});

/// <summary>
///  Run all tests.
/// </summary>
Task("Test")
    .IsDependentOn("Setup")
    .IsDependentOn("BuildTest")
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

            var instanceFolder = CombinePaths(env.Folders.Bin, testConfiguration, testProject, "net46");

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
                    .ExceptionOnError($"Test {testProject} failed for net46");
            }
            else
            {
                // Copy the Mono-built Microsoft.Build.* binaries to the test folder.
                DirectoryHelper.Copy($"{env.Folders.MonoMSBuildLib}", instanceFolder);

                var runScript = CombinePaths(env.Folders.Mono, "run");

                var oldMonoPath = Environment.GetEnvironmentVariable("MONO_PATH");
                try
                {
                    Environment.SetEnvironmentVariable("MONO_PATH", $"{instanceFolder}");

                    // By default, the run script launches OmniSharp. To launch our Mono runtime
                    // with xUnit rather than OmniSharp, we pass '--no-omnisharp'
                    Run(runScript, $"--no-omnisharp \"{xunitInstancePath}\" {arguments}", instanceFolder)
                        .ExceptionOnError($"Test {testProject} failed for net46");
                }
                finally
                {
                    Environment.SetEnvironmentVariable("MONO_PATH", oldMonoPath);
                }
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

    // Copy MSBuild runtime and libraries
    DirectoryHelper.Copy($"{env.Folders.MSBuild}", CombinePaths(outputFolder, "msbuild"));

    // Included in Mono
    FileHelper.Delete(CombinePaths(outputFolder, "System.AppContext.dll"));
    FileHelper.Delete(CombinePaths(outputFolder, "System.Numerics.Vectors.dll"));
    FileHelper.Delete(CombinePaths(outputFolder, "System.Runtime.InteropServices.RuntimeInformation.dll"));
    FileHelper.Delete(CombinePaths(outputFolder, "System.ComponentModel.Primitives.dll"));
    FileHelper.Delete(CombinePaths(outputFolder, "System.ComponentModel.TypeConverter.dll"));
    FileHelper.Delete(CombinePaths(outputFolder, "System.Console.dll"));
    FileHelper.Delete(CombinePaths(outputFolder, "System.IO.FileSystem.Primitives.dll"));
    FileHelper.Delete(CombinePaths(outputFolder, "System.IO.FileSystem.dll"));
    FileHelper.Delete(CombinePaths(outputFolder, "System.Security.Cryptography.Encoding.dll"));
    FileHelper.Delete(CombinePaths(outputFolder, "System.Security.Cryptography.Primitives.dll"));
    FileHelper.Delete(CombinePaths(outputFolder, "System.Security.Cryptography.X509Certificates.dll"));
    FileHelper.Delete(CombinePaths(outputFolder, "System.Threading.Thread.dll"));
}

string PublishMonoBuild(string project, BuildEnvironment env, BuildPlan plan, string configuration, bool archive)
{
    Information($"Publishing Mono build for {project}...");

    var outputFolder = CombinePaths(env.Folders.ArtifactsPublish, project, "mono");

    var buildFolder = CombinePaths(env.Folders.Bin, configuration, project, "net46");

    CopyMonoBuild(env, buildFolder, outputFolder);

    if (archive)
    {
        Package(GetPackagePrefix(project), "mono", outputFolder, env.Folders.ArtifactsPackage);
    }

    return outputFolder;
}

string PublishMonoBuildForPlatform(string project, MonoRuntime monoRuntime, BuildEnvironment env, BuildPlan plan, bool archive)
{
    Information("Publishing platform-specific Mono build: {0}", monoRuntime.PlatformName);

    var outputFolder = CombinePaths(env.Folders.ArtifactsPublish, project, monoRuntime.PlatformName);

    DirectoryHelper.Copy(monoRuntime.InstallFolder, outputFolder);

    Run("chmod", $"+x '{CombinePaths(outputFolder, monoRuntime.RuntimeFile)}'");
    Run("chmod", $"+x '{CombinePaths(outputFolder, "run")}'");

    DirectoryHelper.Copy(env.Folders.MonoFramework, CombinePaths(outputFolder, "framework"));

    var sourceFolder = CombinePaths(env.Folders.ArtifactsPublish, project, "mono");
    var omnisharpFolder = CombinePaths(outputFolder, "omnisharp");

    CopyMonoBuild(env, sourceFolder, omnisharpFolder);

    if (archive)
    {
        Package(GetPackagePrefix(project), monoRuntime.PlatformName, outputFolder, env.Folders.ArtifactsPackage);
    }

    return outputFolder;
}

Task("PublishMonoBuilds")
    .IsDependentOn("Setup")
    .WithCriteria(() => !Platform.Current.IsWindows)
    .Does(() =>
{
    foreach (var project in buildPlan.HostProjects)
    {
        var outputFolder = PublishMonoBuild(project, env, buildPlan, configuration, requireArchive);

        CreateRunScript(project, outputFolder, env.Folders.ArtifactsScripts);

        if (publishAll)
        {
            foreach (var monoRuntime in env.MonoRuntimes)
            {
                PublishMonoBuildForPlatform(project, monoRuntime, env, buildPlan, requireArchive);
            }
        }
    }
});

string PublishWindowsBuild(string project, BuildEnvironment env, BuildPlan plan, string configuration, string rid, bool archive)
{
    var projectName = project + ".csproj";
    var projectFileName = CombinePaths(env.Folders.Source, project, projectName);

    Information("Restoring packages in {0} for {1}...", projectName, rid);

    try
    {
        DotNetCoreRestore(projectFileName, new DotNetCoreRestoreSettings()
        {
            Runtime = rid,
            ToolPath = env.DotNetCommand,
            WorkingDirectory = env.WorkingDirectory,
            Verbosity = DotNetCoreVerbosity.Minimal,
        });
    }
    catch
    {
        Error($"Failed to restore {projectName} for {rid}.");
        throw;
    }

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
    DirectoryHelper.Copy($"{env.Folders.MSBuild}", CombinePaths(outputFolder, "msbuild"));

    if (archive)
    {
        Package(GetPackagePrefix(project), rid, outputFolder, env.Folders.ArtifactsPackage);
    }

    return outputFolder;
}

Task("PublishWindowsBuilds")
    .IsDependentOn("Setup")
    .WithCriteria(() => Platform.Current.IsWindows)
    .Does(() =>
{
    foreach (var project in buildPlan.HostProjects)
    {
        string outputFolder;

        if (publishAll)
        {
            var outputFolder32 = PublishWindowsBuild(project, env, buildPlan, configuration, "win7-x86", requireArchive);
            var outputFolder64 = PublishWindowsBuild(project, env, buildPlan, configuration, "win7-x64", requireArchive);

            outputFolder = Platform.Current.Is32Bit
                ? outputFolder32
                : outputFolder64;
        }
        else if (Platform.Current.Is32Bit)
        {
            outputFolder = PublishWindowsBuild(project, env, buildPlan, configuration, "win7-x86", requireArchive);
        }
        else
        {
            outputFolder = PublishWindowsBuild(project, env, buildPlan, configuration, "win7-x64", requireArchive);
        }

        CreateRunScript(project, outputFolder, env.Folders.ArtifactsScripts);
    }
});

Task("Publish")
    .IsDependentOn("BuildHosts")
    .IsDependentOn("PublishMonoBuilds")
    .IsDependentOn("PublishWindowsBuilds");

/// <summary>
///  Execute the run script.
/// </summary>
Task("ExecuteRunScript")
    .WithCriteria(() => !(Platform.Current.IsMacOS && TravisCI.IsRunningOnTravisCI))
    .Does(() =>
{
    foreach (var project in buildPlan.HostProjects)
    {
        var projectFolder = CombinePaths(env.Folders.Source, project);
        var script = project;
        var scriptPath = CombinePaths(env.Folders.ArtifactsScripts, script);
        var didNotExitWithError = Run(env.ShellCommand, $"{env.ShellArgument}  \"{scriptPath}\" -s \"{projectFolder}\"",
                                    new RunOptions(waitForIdle: true))
                                .WasIdle;
        if (!didNotExitWithError)
        {
            throw new Exception($"Failed to run {script}");
        }
    }
});

/// <summary>
///  Clean install path.
/// </summary>
Task("CleanupInstall")
    .Does(() =>
{
    DirectoryHelper.ForceCreate(installFolder);
});

/// <summary>
///  Quick build.
/// </summary>
Task("Quick")
    .IsDependentOn("Cleanup")
    .IsDependentOn("Publish");

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
    .IsDependentOn("Restore")
    .IsDependentOn("Test")
    .IsDependentOn("Publish")
    .IsDependentOn("ExecuteRunScript");

/// <summary>
///  Default Task aliases to All.
/// </summary>
Task("Default")
    .IsDependentOn("All");

/// <summary>
///  Default to All.
/// </summary>
RunTarget(target);
