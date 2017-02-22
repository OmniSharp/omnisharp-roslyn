#addin "Newtonsoft.Json"

#load "scripts/common.cake"
#load "scripts/runhelpers.cake"
#load "scripts/archiving.cake"
#load "scripts/artifacts.cake"
#load "scripts/nuget.cake"

using System.ComponentModel;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Basic arguments
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

// Optional arguments
var testConfiguration = Argument("test-configuration", "Debug");
var installFolder = Argument("install-path",
    CombinePaths(Environment.GetEnvironmentVariable(IsRunningOnWindows() ? "USERPROFILE" : "HOME"), ".omnisharp", "local"));
var requireArchive = HasArgument("archive");
var useGlobalDotNetSdk = HasArgument("use-global-dotnet-sdk");

var env = new BuildEnvironment(IsRunningOnWindows(), useGlobalDotNetSdk);

/// <summary>
///  Class representing build.json
/// </summary>
public class BuildPlan
{
    public string DotNetInstallScriptURL { get; set; }
    public string DotNetChannel { get; set; }
    public string DotNetVersion { get; set; }
    public string DownloadURL { get; set; }
    public string MSBuildRuntimeForMono { get; set; }
    public string MSBuildLibForMono { get; set; }
    public string[] Frameworks { get; set; }
    public string MainProject { get; set; }
    public string[] TestProjects { get; set; }
    public string[] TestAssetsToRestoreWithNuGet3 { get; set; }

    private string currentRid;
    private string[] targetRids;

    public void SetCurrentRid(string currentRid)
    {
        this.currentRid = currentRid;
    }

    public string[] TargetRids => targetRids;

    public void SetTargetRids(params string[] targetRids)
    {
        this.targetRids = targetRids;
    }

    public string GetDefaultRid()
    {
        if (currentRid.StartsWith("win"))
        {
            return currentRid.EndsWith("-x86")
                ? "win7-x86"
                : "win7-x64";
        }

        // This is a temporary hack to handle the macOS Sierra. At this point,
        // runtime == "default" but the current RID is macOS Sierra (10.12).
        // In that case, fall back to El Capitan (10.11).
        return currentRid == "osx.10.12-x64"
            ? "osx.10.11-x64"
            : currentRid;
    }

    public static BuildPlan Load(BuildEnvironment env)
    {
        var buildJsonPath = PathHelper.Combine(env.WorkingDirectory, "build.json");
        return JsonConvert.DeserializeObject<BuildPlan>(
            System.IO.File.ReadAllText(buildJsonPath));
    }
}

var buildPlan = BuildPlan.Load(env);

// Folders and tools
var msbuildBaseFolder = CombinePaths(env.WorkingDirectory, ".msbuild");
var msbuildNet46Folder = msbuildBaseFolder + "-net46";
var msbuildNetCoreAppFolder = msbuildBaseFolder + "-netcoreapp1.1";
var msbuildRuntimeForMonoInstallFolder = CombinePaths(env.Folders.Tools, "Microsoft.Build.Runtime.Mono");
var msbuildLibForMonoInstallFolder = CombinePaths(env.Folders.Tools, "Microsoft.Build.Lib.Mono");

/// <summary>
///  Clean artifacts.
/// </summary>
Task("Cleanup")
    .Does(() =>
{
    if (DirectoryExists(env.Folders.Artifacts))
    {
        DeleteDirectory(env.Folders.Artifacts, recursive: true);
    }

    CreateDirectory(env.Folders.Artifacts);
    CreateDirectory(env.Folders.ArtifactsLogs);
    CreateDirectory(env.Folders.ArtifactsPackage);
    CreateDirectory(env.Folders.ArtifactsScripts);
});

/// <summary>
///  Pre-build setup tasks.
/// </summary>
Task("Setup")
    .IsDependentOn("BuildEnvironment")
    .IsDependentOn("PopulateRuntimes")
    .IsDependentOn("SetupMSBuild")
    .Does(() =>
{
});

/// <summary>
/// Acquire additional NuGet packages included with OmniSharp (such as MSBuild).
/// </summary>
Task("SetupMSBuild")
    .IsDependentOn("BuildEnvironment")
    .Does(() =>
{
    if (!IsRunningOnWindows())
    {
        if (DirectoryExists(msbuildRuntimeForMonoInstallFolder))
        {
            DeleteDirectory(msbuildRuntimeForMonoInstallFolder, recursive: true);
        }

        if (DirectoryExists(msbuildLibForMonoInstallFolder))
        {
            DeleteDirectory(msbuildLibForMonoInstallFolder, recursive: true);
        }

        CreateDirectory(msbuildRuntimeForMonoInstallFolder);
        CreateDirectory(msbuildLibForMonoInstallFolder);

        var msbuildMonoRuntimeZip = CombinePaths(msbuildRuntimeForMonoInstallFolder, buildPlan.MSBuildRuntimeForMono);
        var msbuildMonoLibZip = CombinePaths(msbuildLibForMonoInstallFolder, buildPlan.MSBuildLibForMono);

        using (var client = new WebClient())
        {
            client.DownloadFile($"{buildPlan.DownloadURL}/{buildPlan.MSBuildRuntimeForMono}", msbuildMonoRuntimeZip);
            client.DownloadFile($"{buildPlan.DownloadURL}/{buildPlan.MSBuildLibForMono}", msbuildMonoLibZip);
        }

        Unzip(msbuildMonoRuntimeZip, msbuildRuntimeForMonoInstallFolder);
        Unzip(msbuildMonoLibZip, msbuildLibForMonoInstallFolder);

        DeleteFile(msbuildMonoRuntimeZip);
        DeleteFile(msbuildMonoLibZip);
    }

    if (DirectoryExists(msbuildNet46Folder))
    {
        DeleteDirectory(msbuildNet46Folder, recursive: true);
    }

    if (DirectoryExists(msbuildNetCoreAppFolder))
    {
        DeleteDirectory(msbuildNetCoreAppFolder, recursive: true);
    }

    CreateDirectory(msbuildNet46Folder);
    CreateDirectory(msbuildNetCoreAppFolder);

    // Copy MSBuild runtime to appropriate locations
    var msbuildInstallFolder = CombinePaths(env.Folders.Tools, "Microsoft.Build.Runtime", "contentFiles", "any");
    var msbuildNet46InstallFolder = CombinePaths(msbuildInstallFolder, "net46");
    var msbuildNetCoreAppInstallFolder = CombinePaths(msbuildInstallFolder, "netcoreapp1.0");

    if (IsRunningOnWindows())
    {
        CopyDirectory(msbuildNet46InstallFolder, msbuildNet46Folder);
    }
    else
    {
        CopyDirectory(msbuildRuntimeForMonoInstallFolder, msbuildNet46Folder);
    }

    CopyDirectory(msbuildNetCoreAppInstallFolder, msbuildNetCoreAppFolder);

    var sdks = new []
    {
        "Microsoft.NET.Sdk",
        "Microsoft.NET.Sdk.Publish",
        "Microsoft.NET.Sdk.Web",
        "Microsoft.NET.Sdk.Web.ProjectSystem",
        "NuGet.Build.Tasks.Pack"
    };

    var net46SdkFolder = CombinePaths(msbuildNet46Folder, "Sdks");
    var netCoreAppSdkFolder = CombinePaths(msbuildNetCoreAppFolder, "Sdks");

    foreach (var sdk in sdks)
    {
        var sdkInstallFolder = CombinePaths(env.Folders.Tools, sdk);
        var net46SdkTargetFolder = CombinePaths(net46SdkFolder, sdk);
        var netCoreAppSdkTargetFolder = CombinePaths(netCoreAppSdkFolder, sdk);

        CopyDirectory(sdkInstallFolder, net46SdkTargetFolder);
        CopyDirectory(sdkInstallFolder, netCoreAppSdkTargetFolder);

        // Ensure that we don't leave the .nupkg unnecessarily hanging around.
        DeleteFiles(CombinePaths(net46SdkTargetFolder, "*.nupkg"));
        DeleteFiles(CombinePaths(netCoreAppSdkTargetFolder, "*.nupkg"));
    }

    // Copy NuGet ImportAfter targets
    var nugetImportAfterTargetsName = "Microsoft.NuGet.ImportAfter.targets";
    var nugetImportAfterTargetsFolder = CombinePaths("15.0", "Microsoft.Common.targets", "ImportAfter");
    var nugetImportAfterTargetsPath = CombinePaths(nugetImportAfterTargetsFolder, nugetImportAfterTargetsName);

    CreateDirectory(CombinePaths(msbuildNet46Folder, nugetImportAfterTargetsFolder));
    CreateDirectory(CombinePaths(msbuildNetCoreAppFolder, nugetImportAfterTargetsFolder));

    CopyFile(CombinePaths(env.Folders.MSBuild, nugetImportAfterTargetsPath), CombinePaths(msbuildNet46Folder, nugetImportAfterTargetsPath));
    CopyFile(CombinePaths(env.Folders.MSBuild, nugetImportAfterTargetsPath), CombinePaths(msbuildNetCoreAppFolder, nugetImportAfterTargetsPath));

    nugetImportAfterTargetsFolder = CombinePaths("15.0", "SolutionFile", "ImportAfter");
    nugetImportAfterTargetsPath = CombinePaths(nugetImportAfterTargetsFolder, nugetImportAfterTargetsName);

    CreateDirectory(CombinePaths(msbuildNet46Folder, nugetImportAfterTargetsFolder));
    CreateDirectory(CombinePaths(msbuildNetCoreAppFolder, nugetImportAfterTargetsFolder));

    CopyFile(CombinePaths(env.Folders.MSBuild, nugetImportAfterTargetsPath), CombinePaths(msbuildNet46Folder, nugetImportAfterTargetsPath));
    CopyFile(CombinePaths(env.Folders.MSBuild, nugetImportAfterTargetsPath), CombinePaths(msbuildNetCoreAppFolder, nugetImportAfterTargetsPath));

    // Copy NuGet.targets from NuGet.Build.Tasks
    var nugetTargetsName = "NuGet.targets";
    var nugetTargetsPath = CombinePaths(env.Folders.Tools, "NuGet.Build.Tasks", "runtimes", "any", "native", nugetTargetsName);

    CopyFile(nugetTargetsPath, CombinePaths(msbuildNet46Folder, nugetTargetsName));
    CopyFile(nugetTargetsPath, CombinePaths(msbuildNetCoreAppFolder, nugetTargetsName));

    // Finally, copy Microsoft.CSharp.Core.targets from Microsoft.Net.Compilers
    var csharpTargetsName = "Microsoft.CSharp.Core.targets";
    var csharpTargetsPath = CombinePaths(env.Folders.Tools, "Microsoft.Net.Compilers", "tools", csharpTargetsName);

    var csharpTargetsNet46Folder = CombinePaths(msbuildNet46Folder, "Roslyn");
    var csharpTargetsNetCoreAppFolder = CombinePaths(msbuildNetCoreAppFolder, "Roslyn");

    CreateDirectory(csharpTargetsNet46Folder);
    CreateDirectory(csharpTargetsNetCoreAppFolder);

    CopyFile(csharpTargetsPath, CombinePaths(csharpTargetsNet46Folder, csharpTargetsName));
    CopyFile(csharpTargetsPath, CombinePaths(csharpTargetsNetCoreAppFolder,csharpTargetsName));
});

/// <summary>
///  Populate the RIDs for the specific environment.
///  Use default RID (+ win7-x86 on Windows) for now.
/// </summary>
Task("PopulateRuntimes")
    .IsDependentOn("BuildEnvironment")
    .Does(() =>
{
    if (IsRunningOnWindows() && string.Equals(Environment.GetEnvironmentVariable("APPVEYOR"), "True"))
    {
        buildPlan.SetTargetRids(
            "default", // To allow testing the published artifact
            "win7-x86",
            "win7-x64");
    }
    else if (string.Equals(Environment.GetEnvironmentVariable("TRAVIS_OS_NAME"), "linux"))
    {
        buildPlan.SetTargetRids(
            "default", // To allow testing the published artifact
            "ubuntu.14.04-x64",
            "ubuntu.16.04-x64",
            "centos.7-x64",
            "rhel.7.2-x64",
            "debian.8-x64",
            "fedora.23-x64",
            "opensuse.13.2-x64");
    }
    else
    {
        // In this case, the build is not happening in CI, so just use the default RID.
        buildPlan.SetTargetRids("default");
    }
});

void ParseDotNetInfoValues(IEnumerable<string> lines, out string version, out string rid, out string basePath)
{
    var keyValueMap = new Dictionary<string, string>();
    foreach (var line in lines)
    {
        var index = line.IndexOf(":");
        if (index >= 0)
        {
            var key = line.Substring(0, index).Trim();
            var value = line.Substring(index + 1).Trim();

            if (!string.IsNullOrEmpty(key) &&
                !string.IsNullOrEmpty(value))
            {
                keyValueMap.Add(key, value);
            }
        }
    }

    if (!keyValueMap.TryGetValue("Version", out version))
    {
        throw new Exception("Could not locate Version in 'dotnet --info' output.");
    }

    if (!keyValueMap.TryGetValue("RID", out rid))
    {
        throw new Exception("Could not locate RID in 'dotnet --info' output.");
    }

    if (!keyValueMap.TryGetValue("Base Path", out basePath))
    {
        throw new Exception("Could not locate Base Path in 'dotnet --info' output.");
    }
}

/// <summary>
///  Install/update build environment.
/// </summary>
Task("BuildEnvironment")
    .Does(() =>
{
    if (!useGlobalDotNetSdk)
    {
        if (!DirectoryExists(env.Folders.DotNetSdk))
        {
            CreateDirectory(env.Folders.DotNetSdk);
        }

        var scriptFileName = $"dotnet-install.{env.ShellScriptFileExtension}";
        var scriptFilePath = CombinePaths(env.Folders.DotNetSdk, scriptFileName);
        var url = $"{buildPlan.DotNetInstallScriptURL}/{scriptFileName}";

        Information("Downloading .NET CLI install script...");

        using (var client = new WebClient())
        {
            client.DownloadFile(url, scriptFilePath);
        }

        if (!IsRunningOnWindows())
        {
            Run("chmod", $"+x '{scriptFilePath}'");
        }

        var argList = new List<string>();

        argList.Add("-Channel");
        argList.Add(buildPlan.DotNetChannel);

        if (!string.IsNullOrEmpty(buildPlan.DotNetVersion))
        {
            argList.Add("-Version");
            argList.Add(buildPlan.DotNetVersion);
        }

        if (!useGlobalDotNetSdk)
        {
            argList.Add("-InstallDir");
            argList.Add(env.Folders.DotNetSdk);
        }

        Information("Launching .NET CLI install script...");

        Run(env.ShellCommand, $"{env.ShellArgument} {scriptFilePath} {string.Join(" ", argList)}");
    }

    // Capture 'dotnet --info' output and parse out RID.
    var lines = new List<string>();

    try
    {
        Run(env.DotNetCommand, "--info", new RunOptions(output: lines));
    }
    catch (Win32Exception)
    {
        throw new Exception("Failed to run 'dotnet --info'");
    }

    string version, rid, basePath;
    ParseDotNetInfoValues(lines, out version, out rid, out basePath);

    buildPlan.SetCurrentRid(rid);

    Information("Using .NET CLI");
    Information("  Version: {0}", version);
    Information("  RID: {0}", rid);
    Information("  Base Path: {0}", basePath);
});

/// <summary>
///  Restore required NuGet packages.
/// </summary>
Task("Restore")
    .IsDependentOn("Setup")
    .Does(() =>
{
    // Restore the projects in OmniSharp.sln
    RunRestore(env.DotNetCommand, "restore OmniSharp.sln", env.WorkingDirectory)
        .ExceptionOnError("Failed to restore projects in OmniSharp.sln.");

    // Restore test assets
    foreach (var project in buildPlan.TestAssetsToRestoreWithNuGet3)
    {
        var folder = CombinePaths(env.Folders.TestAssets, "test-projects", project);
        NuGetRestore(folder);
    }
});

void GetRIDParts(string rid, out string name, out string version, out string arch)
{
    var firstDotIndex = rid.IndexOf('.');
    var lastDashIndex = rid.LastIndexOf('-');

    if (lastDashIndex < 0 ||
        firstDotIndex > lastDashIndex ||
        firstDotIndex + 1 >= rid.Length ||
        lastDashIndex + 1 >= rid.Length)
    {
        throw new ArgumentException($"{nameof(rid)} is not in a valid format: {rid}", nameof(rid));
    }

    if (firstDotIndex == -1)
    {
        name = rid.Substring(0, lastDashIndex);
        version = string.Empty;
    }
    else
    {
        name = rid.Substring(0, firstDotIndex);
        version = rid.Substring(firstDotIndex + 1, lastDashIndex - firstDotIndex - 1);
    }

    arch = rid.Substring(lastDashIndex + 1);
}

void BuildProject(BuildEnvironment env, BuildPlan plan, string projectName, string projectFilePath, string configuration, string rid = null)
{
    rid = rid ?? "default";

    if (rid == "default")
    {
        rid = plan.GetDefaultRid();
    }

    string osName, osVersion, osArch;
    GetRIDParts(rid, out osName, out osVersion, out osArch);

    foreach (var framework in plan.Frameworks)
    {
        var runLog = new List<string>();

        Information($"Building {projectName} on {framework}...");

        if (!IsRunningOnWindows() &&
            !framework.StartsWith("netcore") &&
            !framework.StartsWith("netstandard"))
        {
            Run(env.ShellCommand, $"{env.ShellArgument} msbuild.{env.ShellScriptFileExtension} \"{projectFilePath}\" /p:TargetFramework={framework} /p:Configuration={configuration} /p:OSName={osName} /p:OSVersion={osVersion} /p:OSArch={osArch}",
                    new RunOptions(output: runLog))
                .ExceptionOnError($"Building {projectName} failed for {framework}.");
        }
        else
        {
            Run(env.DotNetCommand, $"build \"{projectFilePath}\" --framework {framework} --configuration {configuration} -p:OSName={osName} -p:OSVersion={osVersion} -p:OSArch={osArch}",
                    new RunOptions(output: runLog))
                .ExceptionOnError($"Building {projectName} failed for {framework}.");
        }

        System.IO.File.WriteAllLines(CombinePaths(env.Folders.ArtifactsLogs, $"{projectName}-{framework}-build.log"), runLog.ToArray());
    }
}

Task("BuildMain")
    .IsDependentOn("Setup")
    .IsDependentOn("Restore")
    .Does(() =>
{
    var projectName = buildPlan.MainProject + ".csproj";
    var projectFilePath = CombinePaths(env.Folders.Source, buildPlan.MainProject, projectName);

    BuildProject(env, buildPlan, projectName, projectFilePath, configuration);
});

/// <summary>
///  Build Test projects.
/// </summary>
Task("BuildTest")
    .IsDependentOn("Setup")
    .IsDependentOn("Restore")
    .IsDependentOn("BuildMain")
    .Does(() =>
{
    foreach (var testProject in buildPlan.TestProjects)
    {
        var testProjectName = testProject + ".csproj";
        var testProjectFilePath = CombinePaths(env.Folders.Tests, testProject, testProjectName);

        BuildProject(env, buildPlan, testProjectName, testProjectFilePath, testConfiguration);
    }
});

/// <summary>
///  Run all tests for .NET Desktop and .NET Core
/// </summary>
Task("TestAll")
    .IsDependentOn("Test")
    .IsDependentOn("TestCore")
    .Does(() =>{});

/// <summary>
///  Run all tests for Travis CI .NET Desktop and .NET Core
/// </summary>
Task("TravisTestAll")
    .IsDependentOn("Cleanup")
    .IsDependentOn("TestAll")
    .Does(() =>{});

/// <summary>
///  Run tests for .NET Core (using .NET CLI).
/// </summary>
Task("TestCore")
    .IsDependentOn("Setup")
    .IsDependentOn("Restore")
    .Does(() =>
{
    foreach (var testProject in buildPlan.TestProjects)
    {
        var logFile = $"{testProject}-core-result.xml";
        var testProjectName = testProject + ".csproj";
        var testProjectFileName = CombinePaths(env.Folders.Tests, testProject, testProjectName);

        Run(env.DotNetCommand, $"test {testProjectFileName} --framework netcoreapp1.1 --logger \"trx;LogFileName={logFile}\" --no-build -- RunConfiguration.ResultsDirectory=\"{env.Folders.ArtifactsLogs}\"")
            .ExceptionOnError($"Test {testProject} failed for .NET Core.");
    }
});

/// <summary>
///  Run tests for other frameworks (using XUnit2).
/// </summary>
Task("Test")
    .IsDependentOn("Setup")
    .IsDependentOn("BuildTest")
    .Does(() =>
{
    foreach (var testProject in buildPlan.TestProjects)
    {
        foreach (var framework in buildPlan.Frameworks)
        {
            // Testing against core happens in TestCore
            if (framework.Contains("netcoreapp"))
            {
                continue;
            }

            var instanceFolder = CombinePaths(env.Folders.Tests, testProject, "bin", testConfiguration, framework);

            // Copy xunit executable to test folder to solve path errors
            var xunitToolsFolder = CombinePaths(env.Folders.Tools, "xunit.runner.console", "tools");
            var xunitInstancePath = CombinePaths(instanceFolder, "xunit.console.exe");
            System.IO.File.Copy(CombinePaths(xunitToolsFolder, "xunit.console.exe"), xunitInstancePath, true);
            System.IO.File.Copy(CombinePaths(xunitToolsFolder, "xunit.runner.utility.net452.dll"), CombinePaths(instanceFolder, "xunit.runner.utility.net452.dll"), true);
            var targetPath = CombinePaths(instanceFolder, $"{testProject}.dll");
            var logFile = CombinePaths(env.Folders.ArtifactsLogs, $"{testProject}-{framework}-result.xml");
            var arguments = $"\"{targetPath}\" -parallel none -xml \"{logFile}\" -notrait category=failing";
            if (IsRunningOnWindows())
            {
                Run(xunitInstancePath, arguments, instanceFolder)
                    .ExceptionOnError($"Test {testProject} failed for {framework}");
            }
            else
            {
                // Copy the Mono-built Microsoft.Build.* binaries to the test folder.
                CopyDirectory($"{msbuildLibForMonoInstallFolder}", instanceFolder);

                Run("mono", $"\"{xunitInstancePath}\" {arguments}", instanceFolder)
                    .ExceptionOnError($"Test {testProject} failed for {framework}");
            }
        }
    }
});

/// <summary>
///  Build, publish and package artifacts.
///  Targets all RIDs specified in build.json unless restricted by RestrictToLocalRuntime.
///  No dependencies on other tasks to support quick builds.
/// </summary>
Task("OnlyPublish")
    .IsDependentOn("Setup")
    .Does(() =>
{
    var project = buildPlan.MainProject;
    var projectName = project + ".csproj";
    var projectFileName = CombinePaths(env.Folders.Source, project, projectName);

    foreach (var framework in buildPlan.Frameworks)
    {
        foreach (var runtime in buildPlan.TargetRids)
        {
            var rid = runtime.Equals("default")
                ? buildPlan.GetDefaultRid()
                : runtime;

            // Restore the OmniSharp.csproj with this runtime.
            RunRestore(env.DotNetCommand, $"restore \"{projectFileName}\" --runtime {rid}", env.WorkingDirectory)
                .ExceptionOnError($"Failed to restore {projectName} for {rid}.");

            var outputFolder = CombinePaths(env.Folders.ArtifactsPublish, project, runtime, framework);
            var argList = new List<string>();

            if (!IsRunningOnWindows() &&
                !framework.StartsWith("netcore") &&
                !framework.StartsWith("netstandard"))
            {
                argList.Add($"\"{projectFileName}\"");
                argList.Add("/t:Publish");
                argList.Add($"/p:RuntimeIdentifier={rid}");
                argList.Add($"/p:TargetFramework={framework}");
                argList.Add($"/p:Configuration={configuration}");
                argList.Add($"/p:PublishDir={outputFolder}");
            }
            else
            {
                argList.Add("publish");

                argList.Add($"\"{projectFileName}\"");

                argList.Add("--runtime");
                argList.Add(rid);

                argList.Add("--framework");
                argList.Add(framework);

                argList.Add("--configuration");
                argList.Add(configuration);

                argList.Add("--output");
                argList.Add($"\"{outputFolder}\"");
            }

            var publishArguments = string.Join(" ", argList);

            if (!IsRunningOnWindows() &&
                !framework.StartsWith("netcore") &&
                !framework.StartsWith("netstandard"))
            {
                Run(env.ShellCommand, $"{env.ShellArgument} msbuild.{env.ShellScriptFileExtension} {publishArguments}")
                    .ExceptionOnError($"Failed to publish {project} / {framework}");
            }
            else
            {
                Run(env.DotNetCommand, publishArguments)
                    .ExceptionOnError($"Failed to publish {project} / {framework}");
            }

            // Copy MSBuild and SDKs to output
            CopyDirectory($"{msbuildBaseFolder}-{framework}", CombinePaths(outputFolder, "msbuild"));

            // For OSX/Linux net46 builds, copy the MSBuild libraries built for Mono.
            if (!IsRunningOnWindows() && framework == "net46")
            {
                CopyDirectory($"{msbuildLibForMonoInstallFolder}", outputFolder);
            }

            if (requireArchive)
            {
                Package(runtime, framework, outputFolder, env.Folders.ArtifactsPackage, buildPlan.MainProject.ToLower());
            }
        }
    }

    CreateRunScript(CombinePaths(env.Folders.ArtifactsPublish, project, "default"), env.Folders.ArtifactsScripts);
});

/// <summary>
///  Alias for OnlyPublish.
///  Targets all RIDs as specified in build.json.
/// </summary>
Task("AllPublish")
    .IsDependentOn("Restore")
    .IsDependentOn("OnlyPublish")
    .Does(() =>
{
});

/// <summary>
///  Restrict the RIDs for the local default.
/// </summary>
Task("RestrictToLocalRuntime")
    .IsDependentOn("Setup")
    .Does(() =>
{
    buildPlan.SetTargetRids("default");
});

/// <summary>
///  Alias for OnlyPublish.
///  Restricts publishing to local RID.
/// </summary>
Task("LocalPublish")
    .IsDependentOn("Restore")
    .IsDependentOn("RestrictToLocalRuntime")
    .IsDependentOn("OnlyPublish")
    .Does(() =>
{
});

/// <summary>
///  Test the published binaries if they start up without errors.
///  Uses builds corresponding to local RID.
/// </summary>
Task("TestPublished")
    .IsDependentOn("Setup")
    .Does(() =>
{
    var project = buildPlan.MainProject;
    var projectFolder = CombinePaths(env.Folders.Source, project);
    var scriptsToTest = new string[] {"OmniSharp", "OmniSharp.Core"};
    foreach (var script in scriptsToTest)
    {
        var scriptPath = CombinePaths(env.Folders.ArtifactsScripts, script);
        var didNotExitWithError = Run(env.ShellCommand, $"{env.ShellArgument}  \"{scriptPath}\" -s \"{projectFolder}\" --stdio",
                                    new RunOptions(timeOut: 10000))
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
    if (System.IO.Directory.Exists(installFolder))
    {
        System.IO.Directory.Delete(installFolder, true);
    }

    System.IO.Directory.CreateDirectory(installFolder);
});

/// <summary>
///  Quick build.
/// </summary>
Task("Quick")
    .IsDependentOn("Cleanup")
    .IsDependentOn("LocalPublish")
    .Does(() =>
{
});

/// <summary>
///  Quick build + install.
/// </summary>
Task("Install")
    .IsDependentOn("Cleanup")
    .IsDependentOn("LocalPublish")
    .IsDependentOn("CleanupInstall")
    .Does(() =>
{
    var project = buildPlan.MainProject;
    foreach (var framework in buildPlan.Frameworks)
    {
        var outputFolder = System.IO.Path.GetFullPath(CombinePaths(env.Folders.ArtifactsPublish, project, "default", framework));
        var targetFolder = System.IO.Path.GetFullPath(CombinePaths(installFolder, framework));
        // Copy all the folders
        foreach (var directory in System.IO.Directory.GetDirectories(outputFolder, "*", SearchOption.AllDirectories))
            System.IO.Directory.CreateDirectory(CombinePaths(targetFolder, directory.Substring(outputFolder.Length + 1)));
        //Copy all the files
        foreach (string file in System.IO.Directory.GetFiles(outputFolder, "*", SearchOption.AllDirectories))
            System.IO.File.Copy(file, CombinePaths(targetFolder, file.Substring(outputFolder.Length + 1)), true);
    }
    CreateRunScript(installFolder, env.Folders.ArtifactsScripts);
});

/// <summary>
///  Full build targeting all RIDs specified in build.json.
/// </summary>
Task("All")
    .IsDependentOn("Cleanup")
    .IsDependentOn("Restore")
    .IsDependentOn("TestAll")
    .IsDependentOn("AllPublish")
    .IsDependentOn("TestPublished")
    .Does(() =>
{
});

/// <summary>
///  Full build targeting local RID.
/// </summary>
Task("Local")
    .IsDependentOn("Cleanup")
    .IsDependentOn("Restore")
    .IsDependentOn("TestAll")
    .IsDependentOn("LocalPublish")
    .IsDependentOn("TestPublished")
    .Does(() =>
{
});

/// <summary>
///  Build centered around producing the final artifacts for Travis
///
///  The tests are run as a different task "TestAll"
/// </summary>
Task("Travis")
    .IsDependentOn("Cleanup")
    .IsDependentOn("Restore")
    .IsDependentOn("AllPublish")
    .IsDependentOn("TestPublished")
    .Does(() =>
{
});

/// <summary>
///  Default Task aliases to Local.
/// </summary>
Task("Default")
    .IsDependentOn("Local")
    .Does(() =>
{
});

/// <summary>
///  Default to Local.
/// </summary>
RunTarget(target);
