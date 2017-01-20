#addin "Newtonsoft.Json"

#load "scripts/pathhelpers.cake"
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

// Working directory
var workingDirectory = System.IO.Directory.GetCurrentDirectory();

// System specific shell configuration
var shell = IsRunningOnWindows() ? "powershell" : "bash";
var shellArgument = IsRunningOnWindows() ? "-NoProfile /Command" : "-C";
var shellExtension = IsRunningOnWindows() ? "ps1" : "sh";

/// <summary>
///  Class representing build.json
/// </summary>
public class BuildPlan
{
    public string[] TestProjects { get; set; }
    public string BuildToolsFolder { get; set; }
    public string ArtifactsFolder { get; set; }
    public string PackagesFolder { get; set; }
    public bool UseSystemDotNetPath { get; set; }
    public string DotNetFolder { get; set; }
    public string DotNetInstallScriptURL { get; set; }
    public string DotNetChannel { get; set; }
    public string DotNetVersion { get; set; }
    public string DownloadURL { get; set; }
    public string MSBuildRuntimeForMono { get; set; }
    public string MSBuildLibForMono { get; set; }
    public string[] Frameworks { get; set; }
    public string[] Rids { get; set; }
    public string MainProject { get; set; }
    public string[] TestProjectsToRestoreWithNuGet3 { get; set; }
    public string CurrentRid { get; set; }
}

var buildPlan = JsonConvert.DeserializeObject<BuildPlan>(
    System.IO.File.ReadAllText(CombinePaths(workingDirectory, "build.json")));

// Folders and tools
var dotnetFolder = CombinePaths(workingDirectory, buildPlan.DotNetFolder);
var dotnetcli = buildPlan.UseSystemDotNetPath ? "dotnet" : CombinePaths(System.IO.Path.GetFullPath(dotnetFolder), "dotnet");
var toolsFolder = CombinePaths(workingDirectory, buildPlan.BuildToolsFolder);

var sourceFolder = CombinePaths(workingDirectory, "src");
var testFolder = CombinePaths(workingDirectory, "tests");
var testAssetsFolder = CombinePaths(workingDirectory, "test-assets");

var artifactFolder = CombinePaths(workingDirectory, buildPlan.ArtifactsFolder);
var publishFolder = CombinePaths(artifactFolder, "publish");
var logFolder = CombinePaths(artifactFolder, "logs");
var packageFolder = CombinePaths(artifactFolder, "package");
var scriptFolder =  CombinePaths(artifactFolder, "scripts");

var packagesFolder = CombinePaths(workingDirectory, buildPlan.PackagesFolder);
var msbuildBaseFolder = CombinePaths(workingDirectory, ".msbuild");
var msbuildNet46Folder = msbuildBaseFolder + "-net46";
var msbuildNetCoreAppFolder = msbuildBaseFolder + "-netcoreapp1.1";
var msbuildRuntimeForMonoInstallFolder = CombinePaths(packagesFolder, "Microsoft.Build.Runtime.Mono");
var msbuildLibForMonoInstallFolder = CombinePaths(packagesFolder, "Microsoft.Build.Lib.Mono");

/// <summary>
///  Clean artifacts.
/// </summary>
Task("Cleanup")
    .Does(() =>
{
    if (DirectoryExists(artifactFolder))
    {
        DeleteDirectory(artifactFolder, recursive: true);
    }

    CreateDirectory(artifactFolder);
    CreateDirectory(logFolder);
    CreateDirectory(packageFolder);
    CreateDirectory(scriptFolder);
});

/// <summary>
///  Pre-build setup tasks.
/// </summary>
Task("Setup")
    .IsDependentOn("BuildEnvironment")
    .IsDependentOn("PopulateRuntimes")
    .IsDependentOn("AcquirePackages")
    .Does(() =>
{
});

/// <summary>
/// Acquire additional NuGet packages included with OmniSharp (such as MSBuild).
/// </summary>
Task("AcquirePackages")
    .IsDependentOn("BuildEnvironment")
    .Does(() =>
{
    var configFilePath = CombinePaths(packagesFolder, "packages.config");

    InstallNuGetPackages(
        configFilePath: configFilePath,
        excludeVersion: true,
        noCache: true,
        outputDirectory: $"\"{packagesFolder}\"");

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
    var msbuildInstallFolder = CombinePaths(packagesFolder, "Microsoft.Build.Runtime", "contentFiles", "any");
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
        var sdkInstallFolder = CombinePaths(packagesFolder, sdk);
        var net46SdkTargetFolder = CombinePaths(net46SdkFolder, sdk);
        var netCoreAppSdkTargetFolder = CombinePaths(netCoreAppSdkFolder, sdk);

        CopyDirectory(sdkInstallFolder, net46SdkTargetFolder);
        CopyDirectory(sdkInstallFolder, netCoreAppSdkTargetFolder);

        // Ensure that we don't leave the .nupkg unnecessarily hanging around.
        DeleteFiles(CombinePaths(net46SdkTargetFolder, "*.nupkg"));
        DeleteFiles(CombinePaths(netCoreAppSdkTargetFolder, "*.nupkg"));
    }

    // Copy NuGet.targets from NuGet.Build.Tasks
    var nugetTargetsName = "NuGet.targets";
    var nugetTargetsPath = CombinePaths(packagesFolder, "NuGet.Build.Tasks", "runtimes", "any", "native", nugetTargetsName);

    CopyFile(nugetTargetsPath, CombinePaths(msbuildNet46Folder, nugetTargetsName));
    CopyFile(nugetTargetsPath, CombinePaths(msbuildNetCoreAppFolder, nugetTargetsName));

    // Finally, copy Microsoft.CSharp.Core.targets from Microsoft.Net.Compilers
    var csharpTargetsName = "Microsoft.CSharp.Core.targets";
    var csharpTargetsPath = CombinePaths(packagesFolder, "Microsoft.Net.Compilers", "tools", csharpTargetsName);

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
        buildPlan.Rids = new string[]
            {
                "default", // To allow testing the published artifact
                "win7-x86",
                "win7-x64"
            };
    }
    else if (string.Equals(Environment.GetEnvironmentVariable("TRAVIS_OS_NAME"), "linux"))
    {
        buildPlan.Rids = new string[]
            {
                "default", // To allow testing the published artifact
                "ubuntu.14.04-x64",
                "ubuntu.16.04-x64",
                "centos.7-x64",
                "rhel.7.2-x64",
                "debian.8-x64",
                "fedora.23-x64",
                "opensuse.13.2-x64"
            };
    }
    else
    {
        // In this case, the build is not happening in CI, so just use the default RID.
        buildPlan.Rids = new string[] {"default"};
    }
});

/// <summary>
///  Install/update build environment.
/// </summary>
Task("BuildEnvironment")
    .Does(() =>
{
    var installScript = $"dotnet-install.{shellExtension}";
    System.IO.Directory.CreateDirectory(dotnetFolder);
    var scriptPath = CombinePaths(dotnetFolder, installScript);
    using (WebClient client = new WebClient())
    {
        client.DownloadFile($"{buildPlan.DotNetInstallScriptURL}/{installScript}", scriptPath);
    }

    if (!IsRunningOnWindows())
    {
        Run("chmod", $"+x '{scriptPath}'");
    }

    var installArgs = $"-Channel {buildPlan.DotNetChannel}";
    if (!String.IsNullOrEmpty(buildPlan.DotNetVersion))
    {
        installArgs = $"{installArgs} -Version {buildPlan.DotNetVersion}";
    }

    if (!buildPlan.UseSystemDotNetPath)
    {
        installArgs = $"{installArgs} -InstallDir {dotnetFolder}";
    }

    Run(shell, $"{shellArgument} {scriptPath} {installArgs}");

    try
    {
        Run(dotnetcli, "--info");
    }
    catch (Win32Exception)
    {
        throw new Exception(".NET CLI binary cannot be found.");
    }

    // Capture 'dotnet --info' output and parse out RID.
    var infoOutput = new List<string>();
    Run(dotnetcli, "--info", new RunOptions { StandardOutputListing = infoOutput });
    foreach (var line in infoOutput)
    {
        var index = line.IndexOf("RID:");
        if (index >= 0)
        {
            buildPlan.CurrentRid = line.Substring(index + "RID:".Length).Trim();
            break;
        }
    }

    System.IO.Directory.CreateDirectory(toolsFolder);

    InstallNuGetPackage(
        packageID: "xunit.runner.console",
        excludeVersion: true,
        noCache: true,
        prerelease: true,
        outputDirectory: $"\"{toolsFolder}\"");
});

/// <summary>
///  Restore required NuGet packages.
/// </summary>
Task("Restore")
    .IsDependentOn("Setup")
    .Does(() =>
{
    // Restore the projects in OmniSharp.sln
    RunRestore(dotnetcli, "restore OmniSharp.sln", workingDirectory)
        .ExceptionOnError("Failed to restore projects in OmniSharp.sln.");

    // Restore test assets
    foreach (var project in buildPlan.TestProjectsToRestoreWithNuGet3)
    {
        var folder = CombinePaths(testAssetsFolder, "test-projects", project);
        NuGetRestore(folder);
    }
});

string GetCurrentRid()
{
    if (buildPlan.CurrentRid.StartsWith("win"))
    {
        return buildPlan.CurrentRid.EndsWith("-x86")
            ? "win7-x86"
            : "win7-x64";
    }

    // This is a temporary hack to handle the macOS Sierra. At this point,
    // runtime == "default" but the current RID is macOS Sierra (10.12).
    // In that case, fall back to El Capitan (10.11).
    return buildPlan.CurrentRid == "osx.10.12-x64"
        ? "osx.10.11-x64"
        : buildPlan.CurrentRid;
}

void GetRIDParts(string rid, out string name, out string version, out string arch)
{
    rid = rid ?? "default";

    if (rid == "default")
    {
        rid = GetCurrentRid();
    }

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
        version = rid.Substring(firstDotIndex + 1, lastDashIndex - firstDotIndex);
    }

    arch = rid.Substring(lastDashIndex + 1);
}

void BuildProject(string projectName, string projectFilePath, string configuration, string rid = null)
{
    string osName, osVersion, osArch;
    GetRIDParts(rid, out osName, out osVersion, out osArch);

    foreach (var framework in buildPlan.Frameworks)
    {
        var runLog = new List<string>();

        Information($"Building {projectName} on {framework}...");

        Run(dotnetcli, $"build \"{projectFilePath}\" --framework {framework} --configuration {configuration} -p:OSName={osName} -p:OSVersion={osVersion} -p:OSArch={osArch}",
                new RunOptions
                {
                    StandardOutputListing = runLog
                })
            .ExceptionOnError($"Building {projectName} failed for {framework}.");

        System.IO.File.WriteAllLines(CombinePaths(logFolder, $"{projectName}-{framework}-build.log"), runLog.ToArray());
    }
}

Task("BuildMain")
    .IsDependentOn("Setup")
    .IsDependentOn("Restore")
    .Does(() =>
{
    var projectName = buildPlan.MainProject + ".csproj";
    var projectFilePath = CombinePaths(sourceFolder, buildPlan.MainProject, projectName);

    BuildProject(projectName, projectFilePath, configuration);
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
        var testProjectFilePath = CombinePaths(testFolder, testProject, testProjectName);

        BuildProject(testProjectName, testProjectFilePath, testConfiguration);
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
        var testProjectFileName = CombinePaths(testFolder, testProject, testProjectName);

        Run(dotnetcli, $"test {testProjectFileName} --framework netcoreapp1.1 --logger \"trx;LogFileName={logFile}\" --no-build -- RunConfiguration.ResultsDirectory=\"{logFolder}\"")
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

            // var frameworkFolder = CombinePaths(testFolder, testProject, "bin", testConfiguration, framework);
            // var runtime = System.IO.Directory.GetDirectories(frameworkFolder).First();
            // var instanceFolder = CombinePaths(frameworkFolder, runtime);
            var instanceFolder = CombinePaths(testFolder, testProject, "bin", testConfiguration, framework);

            // Copy xunit executable to test folder to solve path errors
            var xunitToolsFolder = CombinePaths(toolsFolder, "xunit.runner.console", "tools");
            var xunitInstancePath = CombinePaths(instanceFolder, "xunit.console.exe");
            System.IO.File.Copy(CombinePaths(xunitToolsFolder, "xunit.console.exe"), xunitInstancePath, true);
            System.IO.File.Copy(CombinePaths(xunitToolsFolder, "xunit.runner.utility.desktop.dll"), CombinePaths(instanceFolder, "xunit.runner.utility.desktop.dll"), true);
            var targetPath = CombinePaths(instanceFolder, $"{testProject}.dll");
            var logFile = CombinePaths(logFolder, $"{testProject}-{framework}-result.xml");
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
    var projectFileName = CombinePaths(sourceFolder, project, projectName);

    foreach (var framework in buildPlan.Frameworks)
    {
        foreach (var runtime in buildPlan.Rids)
        {
            var rid = runtime.Equals("default")
                ? GetCurrentRid()
                : runtime;

            // Restore the OmniSharp.csproj with this runtime.
            RunRestore(dotnetcli, $"restore \"{projectFileName}\" --runtime {rid}", workingDirectory)
                .ExceptionOnError($"Failed to restore {projectName} for {rid}.");

            var outputFolder = CombinePaths(publishFolder, project, runtime, framework);
            var argList = new List<string> { "publish" };

            argList.Add($"\"{projectFileName}\"");

            argList.Add("--runtime");
            argList.Add(rid);

            argList.Add("--framework");
            argList.Add(framework);

            argList.Add("--configuration");
            argList.Add(configuration);

            argList.Add("--output");
            argList.Add($"\"{outputFolder}\"");

            var publishArguments = string.Join(" ", argList);

            Run(dotnetcli, publishArguments)
                .ExceptionOnError($"Failed to publish {project} / {framework}");

            // Copy MSBuild and SDKs to output
            CopyDirectory($"{msbuildBaseFolder}-{framework}", CombinePaths(outputFolder, "msbuild"));

            // For OSX/Linux net46 builds, copy the MSBuild libraries built for Mono.
            if (!IsRunningOnWindows() && framework == "net46")
            {
                CopyDirectory($"{msbuildLibForMonoInstallFolder}", outputFolder);
            }

            if (requireArchive)
            {
                Package(runtime, framework, outputFolder, packageFolder, buildPlan.MainProject.ToLower());
            }
        }
    }

    CreateRunScript(CombinePaths(publishFolder, project, "default"), scriptFolder);
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
    buildPlan.Rids = new string[] {"default"};
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
    var projectFolder = CombinePaths(sourceFolder, project);
    var scriptsToTest = new string[] {"OmniSharp", "OmniSharp.Core"};
    foreach (var script in scriptsToTest)
    {
        var scriptPath = CombinePaths(scriptFolder, script);
        var didNotExitWithError = Run($"{shell}", $"{shellArgument}  \"{scriptPath}\" -s \"{projectFolder}\" --stdio",
                                    new RunOptions
                                    {
                                        TimeOut = 10000
                                    })
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
        var outputFolder = System.IO.Path.GetFullPath(CombinePaths(publishFolder, project, "default", framework));
        var targetFolder = System.IO.Path.GetFullPath(CombinePaths(installFolder, framework));
        // Copy all the folders
        foreach (var directory in System.IO.Directory.GetDirectories(outputFolder, "*", SearchOption.AllDirectories))
            System.IO.Directory.CreateDirectory(CombinePaths(targetFolder, directory.Substring(outputFolder.Length + 1)));
        //Copy all the files
        foreach (string file in System.IO.Directory.GetFiles(outputFolder, "*", SearchOption.AllDirectories))
            System.IO.File.Copy(file, CombinePaths(targetFolder, file.Substring(outputFolder.Length + 1)), true);
    }
    CreateRunScript(installFolder, scriptFolder);
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
///  Update the package versions within project.json files.
///  Uses depversion.json file as input.
/// </summary>
Task("SetPackageVersions")
    .Does(() =>
{
    var jDepVersion = JObject.Parse(System.IO.File.ReadAllText(CombinePaths(workingDirectory, "depversion.json")));
    var projects = System.IO.Directory.GetFiles(sourceFolder, "project.json", SearchOption.AllDirectories).ToList();
    projects.AddRange(System.IO.Directory.GetFiles(testFolder, "project.json", SearchOption.AllDirectories));
    foreach (var project in projects)
    {
        var jProject = JObject.Parse(System.IO.File.ReadAllText(project));
        var dependencies = jProject.SelectTokens("dependencies")
                            .Union(jProject.SelectTokens("frameworks.*.dependencies"))
                            .SelectMany(dependencyToken => dependencyToken.Children<JProperty>());
        foreach (JProperty dependency in dependencies)
        {
            if (jDepVersion[dependency.Name] != null)
            {
                dependency.Value = jDepVersion[dependency.Name];
            }
        }
        System.IO.File.WriteAllText(project, JsonConvert.SerializeObject(jProject, Formatting.Indented));
    }
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
