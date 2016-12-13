#addin "Newtonsoft.Json"

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
    System.IO.Path.Combine(Environment.GetEnvironmentVariable(IsRunningOnWindows() ? "USERPROFILE" : "HOME"), ".omnisharp", "local"));
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
    public IDictionary<string, string[]> TestProjects { get; set; }
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
    public string CurrentRid { get; set; }
}

var buildPlan = JsonConvert.DeserializeObject<BuildPlan>(
    System.IO.File.ReadAllText(System.IO.Path.Combine(workingDirectory, "build.json")));

// Folders and tools
var dotnetFolder = System.IO.Path.Combine(workingDirectory, buildPlan.DotNetFolder);
var dotnetcli = buildPlan.UseSystemDotNetPath ? "dotnet" : System.IO.Path.Combine(System.IO.Path.GetFullPath(dotnetFolder), "dotnet");
var toolsFolder = System.IO.Path.Combine(workingDirectory, buildPlan.BuildToolsFolder);

var sourceFolder = System.IO.Path.Combine(workingDirectory, "src");
var testFolder = System.IO.Path.Combine(workingDirectory, "tests");

var artifactFolder = System.IO.Path.Combine(workingDirectory, buildPlan.ArtifactsFolder);
var publishFolder = System.IO.Path.Combine(artifactFolder, "publish");
var logFolder = System.IO.Path.Combine(artifactFolder, "logs");
var packageFolder = System.IO.Path.Combine(artifactFolder, "package");
var scriptFolder =  System.IO.Path.Combine(artifactFolder, "scripts");

var packagesFolder = System.IO.Path.Combine(workingDirectory, buildPlan.PackagesFolder);
var msbuildBaseFolder = System.IO.Path.Combine(workingDirectory, ".msbuild");
var msbuildNet46Folder = msbuildBaseFolder + "-net46";
var msbuildNetCoreAppFolder = msbuildBaseFolder + "-netcoreapp1.0";
var msbuildRuntimeForMonoInstallFolder = System.IO.Path.Combine(packagesFolder, "Microsoft.Build.Runtime.Mono");
var msbuildLibForMonoInstallFolder = System.IO.Path.Combine(packagesFolder, "Microsoft.Build.Lib.Mono");

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
    var configFilePath = System.IO.Path.Combine(packagesFolder, "packages.config");

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

        var msbuildMonoRuntimeZip = System.IO.Path.Combine(msbuildRuntimeForMonoInstallFolder, buildPlan.MSBuildRuntimeForMono);
        var msbuildMonoLibZip = System.IO.Path.Combine(msbuildLibForMonoInstallFolder, buildPlan.MSBuildLibForMono);

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

    // Copy MSBuild and SDKs to appropriate locations
    var msbuildInstallFolder = System.IO.Path.Combine(packagesFolder, "Microsoft.Build.Runtime", "contentFiles", "any");
    var msbuildNet46InstallFolder = System.IO.Path.Combine(msbuildInstallFolder, "net46");
    var msbuildNetCoreAppInstallFolder = System.IO.Path.Combine(msbuildInstallFolder, "netcoreapp1.0");

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

    var net46SdkFolder = System.IO.Path.Combine(msbuildNet46Folder, "Sdks");
    var netCoreAppSdkFolder = System.IO.Path.Combine(msbuildNetCoreAppFolder, "Sdks");

    foreach (var sdk in sdks)
    {
        var sdkInstallFolder = System.IO.Path.Combine(packagesFolder, sdk);
        var net46SdkTargetFolder = System.IO.Path.Combine(net46SdkFolder, sdk);
        var netCoreAppSdkTargetFolder = System.IO.Path.Combine(netCoreAppSdkFolder, sdk);

        CopyDirectory(sdkInstallFolder, net46SdkTargetFolder);
        CopyDirectory(sdkInstallFolder, netCoreAppSdkTargetFolder);

        // Ensure that we don't leave the .nupkg unnecessarily hanging around.
        DeleteFiles(System.IO.Path.Combine(net46SdkTargetFolder, "*.nupkg"));
        DeleteFiles(System.IO.Path.Combine(netCoreAppSdkTargetFolder, "*.nupkg"));
    }

    // Finally, copy Microsoft.CSharp.Core.targets from Microsoft.Net.Compilers
    var targetsName = "Microsoft.CSharp.Core.targets";
    var csharpTargetsPath = System.IO.Path.Combine(packagesFolder, "Microsoft.Net.Compilers", "tools", targetsName);

    CopyFile(csharpTargetsPath, System.IO.Path.Combine(msbuildNet46Folder, targetsName));
    CopyFile(csharpTargetsPath, System.IO.Path.Combine(msbuildNetCoreAppFolder, targetsName));
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
    var scriptPath = System.IO.Path.Combine(dotnetFolder, installScript);
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
    // Restore the folders listed in the global.json
    RunRestore(dotnetcli, "restore", workingDirectory)
        .ExceptionOnError("Failed to restore projects under working directory.");
});

/// <summary>
///  Build Test projects.
/// </summary>
Task("BuildTest")
    .IsDependentOn("Setup")
    .IsDependentOn("Restore")
    .Does(() =>
{
    foreach (var pair in buildPlan.TestProjects)
    {
        foreach (var framework in pair.Value)
        {
            var project = pair.Key;
            var projectFolder = System.IO.Path.Combine(testFolder, project);
            var runLog = new List<string>();

            Information($"Building {project} on {framework}...");

            Run(dotnetcli, $"build --framework {framework} --configuration {testConfiguration} \"{projectFolder}\"",
                    new RunOptions
                    {
                        StandardOutputListing = runLog
                    })
                .ExceptionOnError($"Building test {project} failed for {framework}.");
            System.IO.File.WriteAllLines(System.IO.Path.Combine(logFolder, $"{project}-{framework}-build.log"), runLog.ToArray());
        }
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
    var testProjects = buildPlan.TestProjects
                                .Where(pair => pair.Value.Any(framework => framework.Contains("netcoreapp")))
                                .Select(pair => pair.Key)
                                .ToList();

    foreach (var testProject in testProjects)
    {
        var logFile = System.IO.Path.Combine(logFolder, $"{testProject}-core-result.xml");
        var testWorkingDir = System.IO.Path.Combine(testFolder, testProject);
        Run(dotnetcli, $"test -f netcoreapp1.0 -xml \"{logFile}\" -notrait category=failing", testWorkingDir)
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
    foreach (var pair in buildPlan.TestProjects)
    {
        foreach (var framework in pair.Value)
        {
            // Testing against core happens in TestCore
            if (framework.Contains("netcoreapp"))
            {
                continue;
            }

            var project = pair.Key;
            var frameworkFolder = System.IO.Path.Combine(testFolder, project, "bin", testConfiguration, framework);
            var runtime = System.IO.Directory.GetDirectories(frameworkFolder).First();
            var instanceFolder = System.IO.Path.Combine(frameworkFolder, runtime);

            // Copy xunit executable to test folder to solve path errors
            var xunitToolsFolder = System.IO.Path.Combine(toolsFolder, "xunit.runner.console", "tools");
            var xunitInstancePath = System.IO.Path.Combine(instanceFolder, "xunit.console.exe");
            System.IO.File.Copy(System.IO.Path.Combine(xunitToolsFolder, "xunit.console.exe"), xunitInstancePath, true);
            System.IO.File.Copy(System.IO.Path.Combine(xunitToolsFolder, "xunit.runner.utility.desktop.dll"), System.IO.Path.Combine(instanceFolder, "xunit.runner.utility.desktop.dll"), true);
            var targetPath = System.IO.Path.Combine(instanceFolder, $"{project}.dll");
            var logFile = System.IO.Path.Combine(logFolder, $"{project}-{framework}-result.xml");
            var arguments = $"\"{targetPath}\" -parallel none -xml \"{logFile}\" -notrait category=failing";
            if (IsRunningOnWindows())
            {
                Run(xunitInstancePath, arguments, instanceFolder)
                    .ExceptionOnError($"Test {project} failed for {framework}");
            }
            else
            {
                Run("mono", $"\"{xunitInstancePath}\" {arguments}", instanceFolder)
                    .ExceptionOnError($"Test {project} failed for {framework}");
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
    var projectFolder = System.IO.Path.Combine(sourceFolder, project);
    foreach (var framework in buildPlan.Frameworks)
    {
        foreach (var runtime in buildPlan.Rids)
        {
            var outputFolder = System.IO.Path.Combine(publishFolder, project, runtime, framework);
            var argList = new List<string> { "publish" };

            if (!runtime.Equals("default"))
            {
                argList.Add("--runtime");
                argList.Add(runtime);
            }
            else if (buildPlan.CurrentRid == "osx.10.12-x64")
            {
                // This is a temporary hack to handle the macOS Sierra. At this point,
                // runtime == "default" but the current RID is macOS Sierra (10.12).
                // In that case, fall back to El Capitan (10.11).
                argList.Add("--runtime");
                argList.Add("osx.10.11-x64");
            }

            argList.Add("--framework");
            argList.Add(framework);

            argList.Add("--configuration");
            argList.Add(configuration);

            argList.Add("--output");
            argList.Add($"\"{outputFolder}\"");

            argList.Add($"\"{projectFolder}\"");

            var publishArguments = string.Join(" ", argList);

            Run(dotnetcli, publishArguments)
                .ExceptionOnError($"Failed to publish {project} / {framework}");

            // Copy MSBuild and SDKs to output
            CopyDirectory($"{msbuildBaseFolder}-{framework}", System.IO.Path.Combine(outputFolder, "msbuild"));

            // For OSX/Linux net46 builds, copy the MSBuild libraries built for Mono.
            if (!IsRunningOnWindows() && framework == "net46")
            {
                CopyDirectory($"{msbuildLibForMonoInstallFolder}", outputFolder);

                // Delete a handful of binaries that aren't necessary (and in some cases harmful) when running on Mono.
                DeleteFile(System.IO.Path.Combine(outputFolder, "System.AppContext.dll"));
                DeleteFile(System.IO.Path.Combine(outputFolder, "System.Numerics.Vectors.dll"));
                DeleteFile(System.IO.Path.Combine(outputFolder, "System.Runtime.InteropServices.RuntimeInformation.dll"));
                DeleteFile(System.IO.Path.Combine(outputFolder, "System.ComponentModel.Primitives.dll"));
                DeleteFile(System.IO.Path.Combine(outputFolder, "System.ComponentModel.TypeConverter.dll"));
                DeleteFile(System.IO.Path.Combine(outputFolder, "System.Console.dll"));
                DeleteFile(System.IO.Path.Combine(outputFolder, "System.IO.FileSystem.Primitives.dll"));
                DeleteFile(System.IO.Path.Combine(outputFolder, "System.IO.FileSystem.dll"));
                DeleteFile(System.IO.Path.Combine(outputFolder, "System.Security.Cryptography.Encoding.dll"));
                DeleteFile(System.IO.Path.Combine(outputFolder, "System.Security.Cryptography.Primitives.dll"));
                DeleteFile(System.IO.Path.Combine(outputFolder, "System.Security.Cryptography.X509Certificates.dll"));
                DeleteFile(System.IO.Path.Combine(outputFolder, "System.Threading.Thread.dll"));
            }

            if (requireArchive)
            {
                Package(runtime, framework, outputFolder, packageFolder, buildPlan.MainProject.ToLower());
            }
        }
    }

    CreateRunScript(System.IO.Path.Combine(publishFolder, project, "default"), scriptFolder);
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
    var projectFolder = System.IO.Path.Combine(sourceFolder, project);
    var scriptsToTest = new string[] {"OmniSharp", "OmniSharp.Core"};
    foreach (var script in scriptsToTest)
    {
        var scriptPath = System.IO.Path.Combine(scriptFolder, script);
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
        var outputFolder = System.IO.Path.GetFullPath(System.IO.Path.Combine(publishFolder, project, "default", framework));
        var targetFolder = System.IO.Path.GetFullPath(System.IO.Path.Combine(installFolder, framework));
        // Copy all the folders
        foreach (var directory in System.IO.Directory.GetDirectories(outputFolder, "*", SearchOption.AllDirectories))
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(targetFolder, directory.Substring(outputFolder.Length + 1)));
        //Copy all the files
        foreach (string file in System.IO.Directory.GetFiles(outputFolder, "*", SearchOption.AllDirectories))
            System.IO.File.Copy(file, System.IO.Path.Combine(targetFolder, file.Substring(outputFolder.Length + 1)), true);
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
    var jDepVersion = JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(workingDirectory, "depversion.json")));
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
