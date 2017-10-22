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

var env = new BuildEnvironment(useGlobalDotNetSdk);
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

    string sdkResolverTFM;

    if (Platform.Current.IsWindows)
    {
        Information("Copying MSBuild runtime...");
        var msbuildRuntimeFolder = CombinePaths(env.Folders.Tools, "Microsoft.Build.Runtime", "contentFiles", "any", "net46");
        DirectoryHelper.Copy(msbuildRuntimeFolder, env.Folders.MSBuild);
        sdkResolverTFM = "net46";
    }
    else
    {
        Information("Copying Mono MSBuild runtime...");
        DirectoryHelper.Copy(env.Folders.MonoMSBuildRuntime, env.Folders.MSBuild);
        sdkResolverTFM = "netstandard1.5";
    }

    // Copy MSBuild SDK Resolver and DotNetHostResolver
    Information("Coping MSBuild SDK resolver...");
    var sdkResolverFolder = CombinePaths(env.Folders.Tools, "Microsoft.DotNet.MSBuildSdkResolver", "lib", sdkResolverTFM);
    var msbuildSdkResolverFolder = CombinePaths(env.Folders.MSBuild, "SdkResolvers", "Microsoft.DotNet.MSBuildSdkResolver");
    DirectoryHelper.ForceCreate(msbuildSdkResolverFolder);
    FileHelper.Copy(
        source: CombinePaths(sdkResolverFolder, "Microsoft.DotNet.MSBuildSdkResolver.dll"),
        destination: CombinePaths(msbuildSdkResolverFolder, "Microsoft.DotNet.MSBuildSdkResolver.dll"));

    if (Platform.Current.IsWindows)
    {
        CopyDotNetHostResolver(env, "win", "x86", "hostfxr.dll", msbuildSdkResolverFolder, copyToArchSpecificFolder: true);
        CopyDotNetHostResolver(env, "win", "x64", "hostfxr.dll", msbuildSdkResolverFolder, copyToArchSpecificFolder: true);
    }
    else if (Platform.Current.IsMacOS)
    {
        CopyDotNetHostResolver(env, "osx", "x64", "libhostfxr.dylib", msbuildSdkResolverFolder, copyToArchSpecificFolder: false);
    }
    else if (Platform.Current.IsLinux)
    {
        CopyDotNetHostResolver(env, "linux", "x64", "libhostfxr.so", msbuildSdkResolverFolder, copyToArchSpecificFolder: false);
    }

    // Copy content of Microsoft.Net.Compilers
    Information("Copying Microsoft.Net.Compilers...");
    var compilersFolder = CombinePaths(env.Folders.Tools, "Microsoft.Net.Compilers", "tools");
    var msbuildRoslynFolder = CombinePaths(env.Folders.MSBuild, "Roslyn");

    DirectoryHelper.Copy(compilersFolder, msbuildRoslynFolder);

    // Delete unnecessary files
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "Microsoft.CodeAnalysis.VisualBasic.dll"));
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "Microsoft.VisualBasic.Core.targets"));
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "VBCSCompiler.exe"));
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "VBCSCompiler.exe.config"));
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "vbc.exe"));
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "vbc.exe.config"));
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "vbc.rsp"));
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

    RunTool(env.DotNetCommand, "restore OmniSharp.sln", env.WorkingDirectory)
        .ExceptionOnError("Failed to restore projects in OmniSharp.sln.");
});

/// <summary>
///  Prepare test assets.
/// </summary>
Task("PrepareTestAssets")
    .IsDependentOn("Setup")
    .Does(() =>
{
    // Restore and build test assets
    foreach (var project in buildPlan.TestAssets)
    {
        Information("Restoring and building: {0}...", project);

        var folder = CombinePaths(env.Folders.TestAssets, "test-projects", project);

        RunTool(env.DotNetCommand, "restore", folder)
            .ExceptionOnError($"Failed to restore '{folder}'.");

        RunTool(env.DotNetCommand, "build", folder)
            .ExceptionOnError($"Failed to build '{folder}'.");
    }

    if (AllowLegacyTests())
    {
        var platform = Platform.Current;
        if (platform.IsMacOS && platform.Version.Major == 10 && platform.Version.Minor == 12)
        {
            // Trick to allow older .NET Core SDK to run on Sierra.
            Environment.SetEnvironmentVariable("DOTNET_RUNTIME_ID", "osx.10.11-x64");
        }

        // Restore and build legacy test assets with legacy .NET Core SDK
        foreach (var project in buildPlan.LegacyTestAssets)
        {
            Information("Restoring and building project.json: {0}...", project);

            var folder = CombinePaths(env.Folders.TestAssets, "test-projects", project);

            RunTool(env.LegacyDotNetCommand, "restore", folder)
                .ExceptionOnError($"Failed to restore '{folder}'.");

            RunTool(env.LegacyDotNetCommand, $"build", folder)
                .ExceptionOnError($"Failed to build '{folder}'.");
        }
    }

    // Restore Cake test assets with NuGet
    foreach (var project in buildPlan.CakeTestAssets)
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
    }
});

void BuildProject(BuildEnvironment env, string projectName, string projectFilePath, string configuration, string outputType = null)
{
    string command, arguments;

    // On Windows, we build exclusively with the .NET CLI.
    // On OSX/Linux, we build with the MSBuild installed with Mono.
    if (Platform.Current.IsWindows)
    {
        command = env.DotNetCommand;
        arguments = $"build \"{projectFilePath}\" --no-restore --configuration {configuration} /v:d";
    }
    else
    {
        command = env.ShellCommand;
        arguments = $"{env.ShellArgument} msbuild \"{projectFilePath}\" /p:Configuration={configuration} /v:d";
    }

    var logFileName = CombinePaths(env.Folders.ArtifactsLogs, $"{projectName}-build.log");

    Information("Building {0}...", projectName);

    RunTool(command, arguments, env.WorkingDirectory, logFileName, new Dictionary<string, string>() { { "TestOutputType", outputType } })
        .ExceptionOnError($"Building {projectName} failed.");
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
    DirectoryHelper.Copy($"{env.Folders.MonoMSBuildLib}", outputFolder);

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

    RunTool(env.DotNetCommand, $"restore \"{projectFileName}\" --runtime {rid}", env.WorkingDirectory)
        .ExceptionOnError($"Failed to restore {projectName} for {rid}.");

    var outputFolder = CombinePaths(env.Folders.ArtifactsPublish, project, rid);

    var args = $"publish \"{projectFileName}\" --runtime {rid} --configuration {configuration} --output \"{outputFolder}\"";

    Information("Publishing {0} for {1}...", projectName, rid);

    RunTool(env.DotNetCommand, args, env.WorkingDirectory)
        .ExceptionOnError($"Failed to publish {project} for {rid}");

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
    .Does(() =>
{
    foreach (var project in buildPlan.HostProjects)
    {
        var projectFolder = CombinePaths(env.Folders.Source, project);
        var script = project;
        var scriptPath = CombinePaths(env.Folders.ArtifactsScripts, script);
        var didNotExitWithError = Run(env.ShellCommand, $"{env.ShellArgument}  \"{scriptPath}\" -s \"{projectFolder}\" --stdio",
                                    new RunOptions(timeOut: 30000))
                                .DidTimeOut;
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
