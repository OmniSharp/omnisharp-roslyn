#load "scripts/common.cake"
#load "scripts/runhelpers.cake"
#load "scripts/archiving.cake"
#load "scripts/artifacts.cake"
#load "scripts/platform.cake"
#load "scripts/validation.cake"

using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Xml;

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
var testProjectArgument = Argument("test-project", "");
var testProjects = string.IsNullOrEmpty(testProjectArgument) ? buildPlan.TestProjects : testProjectArgument.Split(',');
var nonCakeTestProjects = buildPlan.TestProjects.Except(new [] { "OmniSharp.Cake.Tests" });

Information("");
Information("Current platform: {0}", Platform.Current);
Information("Test Projects: {0}", string.Join(", ", testProjects));
Information("");

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
    .WithCriteria(!AzurePipelines.IsRunningOnAzurePipelines)
    .Does(() => {
        GitVersion(new GitVersionSettings{
            OutputType = GitVersionOutput.BuildServer
        });
    });

/// <summary>
///  Pre-build setup tasks.
/// </summary>
Task("Setup")
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
        foreach (var dotnetVersion in buildPlan.DotNetVersions)
        {
            InstallDotNetSdk(env, buildPlan,
                version: dotnetVersion,
                installFolder: env.Folders.DotNetSdk);
        }

        // Add non-legacy .NET SDK to PATH
        var oldPath = Environment.GetEnvironmentVariable("PATH");
        var newPath = env.Folders.DotNetSdk + (string.IsNullOrEmpty(oldPath) ? "" : System.IO.Path.PathSeparator + oldPath);
        Environment.SetEnvironmentVariable("PATH", newPath);
        Information("PATH: {0}", Environment.GetEnvironmentVariable("PATH"));
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

Task("CleanUpMonoAssets")
    .WithCriteria(() => !Platform.Current.IsWindows)
    .Does(() =>
{
    if (DirectoryHelper.Exists(env.Folders.Mono))
    {
        DirectoryHelper.Delete(env.Folders.Mono, recursive: true);
    }
});

Task("InstallMonoAssets")
    .WithCriteria(() => !Platform.Current.IsWindows)
    .Does(() =>
{
    if (DirectoryHelper.Exists(env.Folders.Mono))
    {
        Information("Skipping Mono assets installation, because they already exist.");
        return;
    }

    Information("Acquiring Mono runtimes and framework...");

    DownloadFileAndUnzip($"{buildPlan.DownloadURL}/{buildPlan.MonoRuntimeMacOS}", env.Folders.MonoRuntimeMacOS);
    DownloadFileAndUnzip($"{buildPlan.DownloadURL}/{buildPlan.MonoRuntimeLinux32}", env.Folders.MonoRuntimeLinux32);
    DownloadFileAndUnzip($"{buildPlan.DownloadURL}/{buildPlan.MonoRuntimeLinux64}", env.Folders.MonoRuntimeLinux64);

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

    DirectoryHelper.ForceCreate(msbuildCurrentTargetFolder);
    DirectoryHelper.ForceCreate(msbuildCurrentBinTargetFolder);

    var msbuildLibraries = new []
    {
        "Microsoft.Build",
        "Microsoft.Build.Framework",
        "Microsoft.Build.Tasks.Core",
        "Microsoft.Build.Utilities.Core"
    };

    var msbuildRefLibraries = new []
    {
        "Microsoft.Build.Tasks.v4.0",
        "Microsoft.Build.Tasks.v12.0",
        "Microsoft.Build.Utilities.v4.0",
        "Microsoft.Build.Utilities.v12.0",
    };

    // These dependencies are not included in the Microsoft.NET.Sdk package
    // but are necessary for some build tasks.
    var msBuildDependencies = new []
    {
        "Microsoft.Deployment.DotNet.Releases",
        "Microsoft.NET.StringTools",
        "Newtonsoft.Json",
        "System.Threading.Tasks.Dataflow",
        "System.Resources.Extensions"
    };

    // These dependencies are all copied from the Microsoft.NET.Sdk package since
    // we are trying to model a particular SDK's build tools.
    var msBuildSdkDependencies = new []
    {
        "Microsoft.Bcl.AsyncInterfaces",
        "NuGet.Common",
        "NuGet.Configuration",
        "NuGet.DependencyResolver.Core",
        "NuGet.Frameworks",
        "NuGet.LibraryModel",
        "NuGet.Packaging",
        "NuGet.ProjectModel",
        "NuGet.Protocol",
        "NuGet.Versioning",
        "System.Buffers",
        "System.Collections.Immutable",
        "System.Memory",
        "System.Numerics.Vectors",
        "System.Runtime.CompilerServices.Unsafe",
        "System.Text.Encodings.Web",
        "System.Text.Json",
        "System.Threading.Tasks.Extensions",
    };

    var msbuildRuntimeFiles = new []
    {
        "Microsoft.Common.CrossTargeting.targets",
        "Microsoft.Common.CurrentVersion.targets",
        "Microsoft.Common.Mono.targets",
        "Microsoft.Common.overridetasks",
        "Microsoft.Common.targets",
        "Microsoft.Common.tasks",
        "Microsoft.CSharp.CrossTargeting.targets",
        "Microsoft.CSharp.CurrentVersion.targets",
        "Microsoft.CSharp.Mono.targets",
        "Microsoft.CSharp.targets",
        "Microsoft.Data.Entity.targets",
        "Microsoft.Managed.After.targets",
        "Microsoft.Managed.targets",
        "Microsoft.Managed.Before.targets",
        "Microsoft.NET.props",
        "Microsoft.NETFramework.CurrentVersion.props",
        "Microsoft.NETFramework.CurrentVersion.targets",
        "Microsoft.NETFramework.props",
        "Microsoft.NETFramework.targets",
        "Microsoft.ServiceModel.targets",
        "Microsoft.VisualBasic.CrossTargeting.targets",
        "Microsoft.VisualBasic.CurrentVersion.targets",
        "Microsoft.VisualBasic.Mono.targets",
        "Microsoft.VisualBasic.targets",
        "Microsoft.WinFx.targets",
        "Microsoft.WorkflowBuildExtensions.targets",
        "Microsoft.Xaml.targets",
        "MSBuild.dll",
        "MSBuild.dll.config",
        "Workflow.VisualBasic.targets",
        "Workflow.targets",
    };

    if (!Platform.Current.IsWindows)
    {
        // Copy Mono MSBuild files before overwriting with the latest MSBuild from NuGet since Mono requires
        // some extra targets and ref assemblies not present in the NuGet packages.

        var monoBasePath = Platform.Current.IsMacOS
            ? "/Library/Frameworks/Mono.framework/Versions/Current/lib/mono"
            : "/usr/lib/mono";
        var monoMSBuildPath = $"{monoBasePath}/msbuild/Current/bin";
        var monoXBuildPath = $"{monoBasePath}/xbuild/Current";

        Information("Copying Mono MSBuild runtime...");

        var commonTargetsSourcePath = CombinePaths(monoXBuildPath, "Microsoft.Common.props");
        var commonTargetsTargetPath = CombinePaths(msbuildCurrentTargetFolder, "Microsoft.Common.props");
        FileHelper.Copy(commonTargetsSourcePath, commonTargetsTargetPath);

        foreach (var runtimeFileName in msbuildRuntimeFiles)
        {
            var runtimeSourcePath = CombinePaths(monoMSBuildPath, runtimeFileName);
            var runtimeTargetPath = CombinePaths(msbuildCurrentBinTargetFolder, runtimeFileName);
            if (FileHelper.Exists(runtimeSourcePath))
            {
                FileHelper.Copy(runtimeSourcePath, runtimeTargetPath);
            }
        }

        Information("Copying Mono MSBuild Ref Libraries...");

        foreach (var refLibrary in msbuildRefLibraries)
        {
            var refLibraryFileName = refLibrary + ".dll";

            // copy MSBuild Ref Libraries from current Mono
            var refLibrarySourcePath = CombinePaths(monoMSBuildPath, refLibraryFileName);
            var refLibraryTargetPath = CombinePaths(msbuildCurrentBinTargetFolder, refLibraryFileName);
            if (FileHelper.Exists(refLibrarySourcePath))
            {
                FileHelper.Copy(refLibrarySourcePath, refLibraryTargetPath);
            }
        }
    }

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
        if (FileHelper.Exists(librarySourcePath))
        {
            FileHelper.Copy(librarySourcePath, libraryTargetPath);
        }
    }

    Information("Copying MSBuild dependencies...");

    foreach (var dependency in msBuildDependencies)
    {
        var dependencyFileName = dependency + ".dll";
        var dependencySourcePath = CombinePaths(env.Folders.Tools, dependency, "lib", "netstandard2.0", dependencyFileName);
        var dependencyTargetPath = CombinePaths(msbuildCurrentBinTargetFolder, dependencyFileName);
        if (FileHelper.Exists(dependencySourcePath))
        {
            FileHelper.Copy(dependencySourcePath, dependencyTargetPath);
        }
    }

    Information("Copying MSBuild SDK dependencies...");

    var sdkToolsSourceFolder = CombinePaths(env.Folders.Tools, "Microsoft.NET.Sdk", "tools", "net472");
    foreach (var dependency in msBuildSdkDependencies)
    {
        var dependencyFileName = dependency + ".dll";
        var dependencySourcePath = CombinePaths(sdkToolsSourceFolder, dependencyFileName);
        var dependencyTargetPath = CombinePaths(msbuildCurrentBinTargetFolder, dependencyFileName);
        if (FileHelper.Exists(dependencySourcePath))
        {
            FileHelper.Copy(dependencySourcePath, dependencyTargetPath);
        }
    }

    // Copy MSBuild SDK Resolver and DotNetHostResolver
    Information("Copying MSBuild SDK resolver...");
    var msbuildSdkResolverSourceFolder = CombinePaths(env.Folders.Tools, "Microsoft.DotNet.MSBuildSdkResolver", "lib", "net472");
    var msbuildSdkResolverTargetFolder = CombinePaths(msbuildCurrentBinTargetFolder, "SdkResolvers", "Microsoft.DotNet.MSBuildSdkResolver");
    DirectoryHelper.ForceCreate(msbuildSdkResolverTargetFolder);
    FileHelper.Copy(
        source: CombinePaths(msbuildSdkResolverSourceFolder, "Microsoft.DotNet.MSBuildSdkResolver.dll"),
        destination: CombinePaths(msbuildSdkResolverTargetFolder, "Microsoft.DotNet.MSBuildSdkResolver.dll"));

    // Add sentinel file to enable workload resolver
    FileHelper.WriteAllLines(
        path: CombinePaths(msbuildSdkResolverTargetFolder, "EnableWorkloadResolver.sentinel"),
        contents: new string[0]
    );

    if (Platform.Current.IsWindows)
    {
        CopyDotNetHostResolver(env, "win", "x86", "hostfxr.dll", msbuildSdkResolverTargetFolder, copyToArchSpecificFolder: true);
        CopyDotNetHostResolver(env, "win", "x64", "hostfxr.dll", msbuildSdkResolverTargetFolder, copyToArchSpecificFolder: true);
        CopyDotNetHostResolver(env, "win", "arm64", "hostfxr.dll", msbuildSdkResolverTargetFolder, copyToArchSpecificFolder: true);
    }
    else if (Platform.Current.IsMacOS)
    {
        CopyDotNetHostResolver(env, "osx", "x64", "libhostfxr.dylib", msbuildSdkResolverTargetFolder, copyToArchSpecificFolder: false);
    }
    else if (Platform.Current.IsLinux)
    {
        CopyDotNetHostResolver(env, "linux", "x64", "libhostfxr.so", msbuildSdkResolverTargetFolder, copyToArchSpecificFolder: false);
    }

    Information("Copying NuGet SDK resolver...");
    var nugetSdkResolverSourceFolder = CombinePaths(env.Folders.Tools, "Microsoft.Build.NuGetSdkResolver", "lib", "net472");
    var nugetSdkResolverTargetFolder = CombinePaths(msbuildCurrentBinTargetFolder, "SdkResolvers", "Microsoft.Build.NuGetSdkResolver");
    DirectoryHelper.ForceCreate(nugetSdkResolverTargetFolder);
    FileHelper.Copy(
        source: CombinePaths(nugetSdkResolverSourceFolder, "Microsoft.Build.NuGetSdkResolver.dll"),
        destination: CombinePaths(msbuildCurrentBinTargetFolder, "Microsoft.Build.NuGetSdkResolver.dll"));
    FileHelper.WriteAllLines(
        path: CombinePaths(nugetSdkResolverTargetFolder, "Microsoft.Build.NuGetSdkResolver.xml"),
        contents: new [] {
            "<SdkResolver>",
            "  <Path>../../Microsoft.Build.NuGetSdkResolver.dll</Path>",
            "</SdkResolver>"
        }
    );

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

    // Copy dependencies of NuGet.Build.Tasks & Microsoft.Build.NuGetSdkResolver
    var nugetPackages = new []
    {
        "NuGet.Commands",
        "NuGet.Credentials"
    };

    foreach (var nugetPackage in nugetPackages)
    {
        var binaryName = nugetPackage + ".dll";

        FileHelper.Copy(
            source: CombinePaths(env.Folders.Tools, nugetPackage, "lib", "net472", binaryName),
            destination: CombinePaths(msbuildCurrentBinTargetFolder, binaryName));
    }

    // Copy content of Microsoft.Net.Compilers.Toolset
    Information("Copying Microsoft.Net.Compilers.Toolset...");
    var compilersSourceFolder = CombinePaths(env.Folders.Tools, "Microsoft.Net.Compilers.Toolset", "tasks", "net472");
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
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.core", "lib", "netstandard2.0", "SQLitePCLRaw.core.dll"),
        destination: CombinePaths(msbuildCurrentBinTargetFolder, "SQLitePCLRaw.core.dll"),
        overwrite: true);

    FileHelper.Copy(
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.provider.e_sqlite3", "lib", "netstandard2.0", "SQLitePCLRaw.provider.e_sqlite3.dll"),
        destination: CombinePaths(msbuildCurrentBinTargetFolder, "SQLitePCLRaw.provider.e_sqlite3.dll"),
        overwrite: true);

    FileHelper.Copy(
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.bundle_green", "lib", "netstandard2.0", "SQLitePCLRaw.batteries_v2.dll"),
        destination: CombinePaths(msbuildCurrentBinTargetFolder, "SQLitePCLRaw.batteries_v2.dll"),
        overwrite: true);
});

/// <summary>
///  Prepare test assets.
/// </summary>
Task("PrepareTestAssets")
    .IsDependentOn("Setup");

Task("PrepareTestAssets:CommonTestAssets")
    .IsDependeeOf("PrepareTestAssets")
    .WithCriteria(testProjects.Any(z => nonCakeTestProjects.Any(x => x == z)))
    .DoesForEach(buildPlan.TestAssets, (project) =>
    {
        Information("Restoring and building: {0}...", project);

        var folder = CombinePaths(env.Folders.TestAssets, "test-projects", project);

        try {
            DotNetCoreBuild(folder, new DotNetCoreBuildSettings()
            {
                ToolPath = env.DotNetCommand,
                WorkingDirectory = folder,
                Verbosity = DotNetCoreVerbosity.Minimal
            });
        } catch {
            // ExternalAlias has issues once in a while, try building again to get it working.
            if (project == "ExternAlias") {

                DotNetCoreBuild(folder, new DotNetCoreBuildSettings()
                {
                    ToolPath = env.DotNetCommand,
                    WorkingDirectory = folder,
                    Verbosity = DotNetCoreVerbosity.Minimal
                });
            }
        }
    });

Task("PrepareTestAssets:RestoreOnlyTestAssets")
    .IsDependeeOf("PrepareTestAssets")
    .WithCriteria(testProjects.Any(z => nonCakeTestProjects.Any(x => x == z)))
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
    .WithCriteria(testProjects.Any(z => nonCakeTestProjects.Any(x => x == z)))
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

Task("PrepareTestAssets:CakeTestAssets")
    .IsDependeeOf("PrepareTestAssets")
    .WithCriteria(testProjects.Contains("OmniSharp.Cake.Tests"))
    .DoesForEach(buildPlan.CakeTestAssets, (project) =>
    {
        Information("Restoring: {0}...", project);

        var toolsFolder = CombinePaths(env.Folders.TestAssets, "test-projects", project, "tools");
        var packagesConfig = CombinePaths(toolsFolder, "packages.config");

        if (!Platform.Current.IsWindows)
        {
            Warning($"TestAssets: {toolsFolder}");
            Run("chmod", $"777 {toolsFolder}/");
            Information($"TestAssets: {toolsFolder}");
        }

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

Task("Build")
    .IsDependentOn("Setup")
    .Does(() =>
{
    try
    {
        BuildWithDotNetCli(env, configuration);
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
        foreach (var testProject in testProjects)
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
                DirectoryHelper.Copy($"{env.Folders.MSBuild}/Current/Bin", instanceFolder);

                var runScript = CombinePaths(env.Folders.Mono, "run");

                // By default, the run script launches OmniSharp. To launch our Mono runtime
                // with xUnit rather than OmniSharp, we pass '--no-omnisharp'
                Run(runScript, $"--no-omnisharp \"{xunitInstancePath}\" {arguments}", instanceFolder)
                    .ExceptionOnError($"Test {testProject} failed for net472");
            }
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
}

void CopyExtraDependencies(BuildEnvironment env, string outputFolder)
{
    // copy license
    FileHelper.Copy(CombinePaths(env.WorkingDirectory, "license.md"), CombinePaths(outputFolder, "license.md"), overwrite: true);
}

void AddOmniSharpBindingRedirects(string omnisharpFolder)
{
    var appConfig = CombinePaths(omnisharpFolder, "OmniSharp.exe.config");

    // Load app.config
    var document = new XmlDocument();
    document.Load(appConfig);

    // Find bindings
    var runtime = document.GetElementsByTagName("runtime")[0];
    var assemblyBinding = document.CreateElement("assemblyBinding", "urn:schemas-microsoft-com:asm.v1");

    // Find OmniSharp libraries
    foreach (var filePath in System.IO.Directory.GetFiles(omnisharpFolder, "OmniSharp.*.dll"))
    {
        // Read assembly name from OmniSharp library
        var assemblyName = AssemblyName.GetAssemblyName(filePath);

        // Create binding redirect and add to bindings
        var redirect = CreateBindingRedirect(document, assemblyName);
        assemblyBinding.AppendChild(redirect);
    }

    runtime.AppendChild(assemblyBinding);

    // Save updated app.config
    document.Save(appConfig);
}

XmlElement CreateBindingRedirect(XmlDocument document, AssemblyName assemblyName)
{
    var dependentAssembly = document.CreateElement("dependentAssembly", "urn:schemas-microsoft-com:asm.v1");

    var assemblyIdentity = document.CreateElement("assemblyIdentity", "urn:schemas-microsoft-com:asm.v1");
    assemblyIdentity.SetAttribute("name", assemblyName.Name);
    var publicKeyToken = BitConverter.ToString(assemblyName.GetPublicKeyToken()).Replace("-", string.Empty).ToLower();
    assemblyIdentity.SetAttribute("publicKeyToken", publicKeyToken);
    assemblyIdentity.SetAttribute("culture", "neutral");
    dependentAssembly.AppendChild(assemblyIdentity);

    var bindingRedirect = document.CreateElement("bindingRedirect", "urn:schemas-microsoft-com:asm.v1");
    bindingRedirect.SetAttribute("oldVersion", $"0.0.0.0-{assemblyName.Version}");
    bindingRedirect.SetAttribute("newVersion", assemblyName.Version.ToString());
    dependentAssembly.AppendChild(bindingRedirect);

    return dependentAssembly;
}

string PublishMonoBuild(string project, BuildEnvironment env, BuildPlan plan, string configuration)
{
    Information($"Publishing Mono build for {project}...");

    var outputFolder = CombinePaths(env.Folders.ArtifactsPublish, project, "mono");

    var buildFolder = CombinePaths(env.Folders.Bin, configuration, project, "net472");

    CopyMonoBuild(env, buildFolder, outputFolder);

    CopyExtraDependencies(env, outputFolder);
    AddOmniSharpBindingRedirects(outputFolder);

    // Copy dependencies of Mono build
    FileHelper.Copy(
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.core", "lib", "netstandard2.0", "SQLitePCLRaw.core.dll"),
        destination: CombinePaths(outputFolder, "SQLitePCLRaw.core.dll"),
        overwrite: true);
    FileHelper.Copy(
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.provider.e_sqlite3", "lib", "netstandard2.0", "SQLitePCLRaw.provider.e_sqlite3.dll"),
        destination: CombinePaths(outputFolder, "SQLitePCLRaw.provider.e_sqlite3.dll"),
        overwrite: true);
    FileHelper.Copy(
        source: CombinePaths(env.Folders.Tools, "SQLitePCLRaw.bundle_green", "lib", "netstandard2.0", "SQLitePCLRaw.batteries_v2.dll"),
        destination: CombinePaths(outputFolder, "SQLitePCLRaw.batteries_v2.dll"),
        overwrite: true);

    Package(project, "mono", outputFolder, env.Folders.ArtifactsPackage, env.Folders.DeploymentPackage);

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
    AddOmniSharpBindingRedirects(omnisharpFolder);

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
            // Runtime = rid, // TODO: With everything today do we need to publish this with a rid?  This appears to be legacy bit when we used to push for all supported dotnet core rids.
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
    AddOmniSharpBindingRedirects(outputFolder);

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
            var outputFolderX86 = PublishWindowsBuild(project, env, buildPlan, configuration, "win7-x86");
            var outputFolderX64 = PublishWindowsBuild(project, env, buildPlan, configuration, "win7-x64");
            var outputFolderArm64 = PublishWindowsBuild(project, env, buildPlan, configuration, "win10-arm64");

            outputFolder = Platform.Current.IsX86
                ? outputFolderX86
                : Platform.Current.IsX64
                    ? outputFolderX64
                    : outputFolderArm64;
        }
        else if (Platform.Current.IsX86)
        {
            outputFolder = PublishWindowsBuild(project, env, buildPlan, configuration, "win7-x86");
        }
        else if (Platform.Current.IsX64)
        {
            outputFolder = PublishWindowsBuild(project, env, buildPlan, configuration, "win7-x64");
        }
        else
        {
            outputFolder = PublishWindowsBuild(project, env, buildPlan, configuration, "win10-arm64");
        }

        CreateRunScript(project, outputFolder, env.Folders.ArtifactsScripts);
    }
});

Task("PublishNuGet")
    .IsDependentOn("InstallDotNetCoreSdk")
    .Does(() => {
        DotNetCorePack(".", new DotNetCorePackSettings() {
            Configuration = "Release",
            OutputDirectory = "./artifacts/nuget/",
            MSBuildSettings = new DotNetCoreMSBuildSettings()
                .SetConfiguration(configuration)
                .WithProperty("PackageVersion", env.VersionInfo.NuGetVersion)
                .WithProperty("AssemblyVersion", env.VersionInfo.AssemblySemVer)
                .WithProperty("FileVersion", env.VersionInfo.AssemblySemVer)
                .WithProperty("InformationalVersion", env.VersionInfo.InformationalVersion),
        });
    });

Task("Publish")
    .IsDependentOn("Build")
    .IsDependentOn("PublishMonoBuilds")
    .IsDependentOn("PublishWindowsBuilds")
    .IsDependentOn("PublishNuGet");

/// <summary>
///  Execute the run script.
/// </summary>
Task("ExecuteRunScript")
    .Does(() =>
{
    // TODO: Pass configuration into run script to ensure that MSBuild output paths are handled correctly.
    // Otherwise, we get "could not resolve output path" messages when building for release.

    foreach (var project in buildPlan.HostProjects)
    {
        var projectFolder = CombinePaths(env.Folders.Source, project);

        var scriptPath = GetScriptPath(env.Folders.ArtifactsScripts, project);
        var didNotExitWithError = Run(env.ShellCommand, $"{env.ShellArgument} \"{scriptPath}\" -s \"{projectFolder}\"",
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
            platform = Platform.Current.IsX86
                ? "win7-x86"
                : Platform.Current.IsX64
                    ? "win7-x64"
                    : "win10-arm64";
        }
        else
        {
            platform = "mono";
        }

        var outputFolder = PathHelper.GetFullPath(CombinePaths(env.Folders.ArtifactsPublish, project, platform));
        var targetFolder = PathHelper.GetFullPath(CombinePaths(installFolder, project));

        DirectoryHelper.Copy(outputFolder, targetFolder);

        CreateRunScript(project, CombinePaths(installFolder, project), env.Folders.ArtifactsScripts);

        Information($"OmniSharp is installed locally at {installFolder}");
    }
});

/// <summary>
///  Full build and execute script at the end.
/// </summary>
Task("All")
    .IsDependentOn("Cleanup")
    .IsDependentOn("CleanUpMonoAssets")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Publish")
    .IsDependentOn("ExecuteRunScript");

/// <summary>
///  Default Task aliases to All.
/// </summary>
Task("Default")
    .IsDependentOn("All");

/// <summary>
///  Task aliases for CI (excluding tests) as they are parallelized
/// </summary>
Task("CI")
    .IsDependentOn("Cleanup")
    .IsDependentOn("CleanUpMonoAssets")
    .IsDependentOn("Build")
    .IsDependentOn("Publish")
    .IsDependentOn("ExecuteRunScript");

Teardown(context =>
{
    // Ensure that we shutdown all build servers used by the CLI during build.
    Run(env.DotNetCommand, "build-server shutdown");
});

/// <summary>
///  Default to All.
/// </summary>
RunTarget(target);
