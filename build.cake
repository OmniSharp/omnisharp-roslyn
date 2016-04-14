#addin "Newtonsoft.Json"

using System.Text.RegularExpressions;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Basic arguments
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
// Optional arguments
var testConfiguration = Argument("test-configuration", "Debug");
var installFolder = Argument("install-path", IsRunningOnWindows() ?
                        $"{EnvironmentVariable("USERPROFILE")}/.omnisharp/local" :
                        $"{EnvironmentVariable("HOME")}/.omnisharp/local");
var requireArchive = HasArgument("archive");

// Working directory
var workingDirectory = (new CakeEnvironment()).WorkingDirectory;

// System specific shell configuration
var shell = IsRunningOnWindows() ? "powershell" : "bash";
var shellArgument = IsRunningOnWindows() ? "/Command" : "-C";
var shellExtension = IsRunningOnWindows() ? "ps1" : "sh";

/// <summary>
///  Class representing build.json
/// </summary>
public class BuildPlan
{
    public IDictionary<string, string[]> TestProjects { get; set; }
    public string BuildToolsFolder { get; set; }
    public string ArtifactsFolder { get; set; }
    public bool UseSystemDotNetPath { get; set; }
    public string DotNetFolder { get; set; }
    public string DotNetInstallScriptURL { get; set; }
    public string DotNetChannel { get; set; }
    public string DotNetVersion { get; set; }
    public string[] Frameworks { get; set; }
    public string[] Rids { get; set; }
    public string MainProject { get; set; }
}

var buildPlan = JsonConvert.DeserializeObject<BuildPlan>(
    System.IO.File.ReadAllText($"{workingDirectory}/build.json"));

// Folders and tools
var dotnetFolder = $"{workingDirectory}/{buildPlan.DotNetFolder}";
var dotnetcli = buildPlan.UseSystemDotNetPath ? "dotnet" :
                    $"{dotnetFolder}/dotnet";
var toolsFolder = $"{workingDirectory}/{buildPlan.BuildToolsFolder}";
var xunitRunner = "xunit.runner.console";

var sourceFolder = $"{workingDirectory}/src";
var testFolder = $"{workingDirectory}/tests";

var artifactFolder = $"{workingDirectory}/{buildPlan.ArtifactsFolder}";
var publishFolder = $"{artifactFolder}/publish";
var logFolder = $"{artifactFolder}/logs";
var packageFolder = $"{artifactFolder}/package";

/// <summary>
///  Generate an archive out of the given published folder.
///  Use ZIP for Windows runtimes.
///  Use TAR.GZ for non-Windows runtimes.
///  Use 7z to generate TAR.GZ on Windows if available.
/// </summary>
/// <param name="runtime">The runtime targeted by the published folder</param>
/// <param name="inputFolder">The published folder</param>
/// <param name="outputFile">The target archive name (without extension)</param>
void DoArchive(string runtime, DirectoryPath inputFolder, FilePath outputFile)
{
    // On all platforms use ZIP for Windows runtimes
    if (runtime.Contains("win") || (runtime.Equals("default") && IsRunningOnWindows()))
    {
        var zipFile = outputFile.AppendExtension("zip");
        Zip(inputFolder, zipFile);
    }
    // On all platforms use TAR.GZ for Unix runtimes
    else
    {
        var tarFile = outputFile.AppendExtension("tar.gz");
        // Use 7z to create TAR.GZ on Windows
        if (IsRunningOnWindows())
        {
            var tempFile = outputFile.AppendExtension("tar");
            try
            {
                var exitCode = StartProcess("7z",
                    new ProcessSettings
                    {
                        Arguments = $"a {tempFile}",
                        WorkingDirectory = inputFolder
                    });
                if (exitCode != 0)
                {
                    throw new Exception($"Tar-ing failed for {inputFolder} {outputFile}");
                }
                exitCode = StartProcess("7z",
                    new ProcessSettings
                    {
                        Arguments = $"a {tarFile} {tempFile}",
                        WorkingDirectory = inputFolder
                    });
                if (exitCode != 0)
                {
                    throw new Exception($"Compression failed for {inputFolder} {outputFile}");
                }
                DeleteFile(tempFile);
            }
            catch(Win32Exception)
            {
                Information("Warning: 7z not available on PATH to pack tar.gz results");
            }
        }
        // Use tar to create TAR.GZ on Unix
        else
        {
            var exitCode =  StartProcess("tar",
                new ProcessSettings
                {
                    Arguments = $"czf {tarFile} .",
                    WorkingDirectory = inputFolder
                });
            if (exitCode != 0)
            {
                throw new Exception($"Compression failed for {inputFolder} {outputFile}");
            }
        }
    }
}

/// <summary>
///  Extract the RID from a generated build folder.
///  Used when targeting unknown RID (for example during testing).
///  Throws exception when multiple RID folders found.
/// </summary>
/// <param name="path">Build path including RID folders</param>
string GetRuntimeInPath(string path)
{
    return GetDirectories(path).First().GetDirectoryName();
}

/// <summary>
///  Clean artifacts.
/// </summary>
Task("Cleanup")
    .Does(() =>
{
    if (DirectoryExists(artifactFolder))
    {
        CleanDirectory(artifactFolder);
    }
    else
    {
        CreateDirectory(artifactFolder);
    }
    CreateDirectory(logFolder);
    CreateDirectory(packageFolder);
});

/// <summary>
///  Pre-build setup tasks.
/// </summary>
Task("Setup")
    .IsDependentOn("BuildEnvironment")
    .IsDependentOn("PopulateRuntimes")
    .Does(() =>
{
});

/// <summary>
///  Populate the RIDs for the specific environment.
///  Use default RID (+ win7-x86 on Windows) for now.
/// </summary>
Task("PopulateRuntimes")
    .IsDependentOn("BuildEnvironment")
    .Does(() =>
{
    if (IsRunningOnWindows())
    {
        buildPlan.Rids = new string[] {"default", "win7-x86"};
    }
    else if (string.Equals(EnvironmentVariable("TRAVIS_OS_NAME"), "linux"))
    {
        buildPlan.Rids = new string[]
            {
                "ubuntu.14.04-x64",
                "centos.7-x64",
                "rhel.7.2-x64",
                "debian.8.2-x64"
            };
    }
    else
    {
        buildPlan.Rids = new string[] {"default"};
    }
});

/// <summary>
///  Install/update build environment.
/// </summary>
Task("BuildEnvironment")
    .Does(() =>
{
    var installScript = $"install.{shellExtension}";
    CreateDirectory(dotnetFolder);
    var scriptPath = new FilePath($"{dotnetFolder}/{installScript}");
    DownloadFile($"{buildPlan.DotNetInstallScriptURL}/{installScript}", scriptPath);
    if (!IsRunningOnWindows())
    {
        StartProcess("chmod",
            new ProcessSettings
            {
                Arguments = $"+x {scriptPath}"
            });
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
    StartProcess(shell,
        new ProcessSettings
        {
            Arguments = $"{shellArgument} {scriptPath} {installArgs}"
        });
    try
    {
        StartProcess(dotnetcli,
            new ProcessSettings
            {
                Arguments = "--info"
            });

    }
    catch (Win32Exception)
    {
        throw new Exception(".NET CLI binary cannot be found.");
    }

    CreateDirectory(toolsFolder);

    NuGetInstall(xunitRunner,
        new NuGetInstallSettings
        {
            ExcludeVersion  = true,
            OutputDirectory = toolsFolder,
            NoCache = true,
            Prerelease = true
        });
});

/// <summary>
///  Restore required NuGet packages.
/// </summary>
Task("Restore")
    .IsDependentOn("Setup")
    .Does(() =>
{
    var exitCode = StartProcess(dotnetcli,
        new ProcessSettings
        {
            Arguments = "restore"
        });
    if (exitCode != 0)
    {
        throw new Exception("Failed to restore.");
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
    foreach (var pair in buildPlan.TestProjects)
    {
        foreach (var framework in pair.Value)
        {
            var project = pair.Key;
            var process = StartAndReturnProcess(dotnetcli,
                new ProcessSettings
                {
                    Arguments = $"build --framework {framework} --configuration {testConfiguration} {testFolder}/{project}",
                    RedirectStandardOutput = true
                });
            process.WaitForExit();
            System.IO.File.WriteAllLines($"{logFolder}/{project}-{framework}-build.log", process.GetStandardOutput().ToArray());
        }
    }
});


/// <summary>
///  Run tests for .NET Core (using .NET CLI).
/// </summary>
Task("TestCore")
    .IsDependentOn("Setup")
    .IsDependentOn("BuildTest")
    .Does(() =>
{
    foreach (var pair in buildPlan.TestProjects)
    {
        foreach (var framework in pair.Value)
        {
            if (!framework.Contains("netcoreapp"))
            {
                continue;
            }

            var project = pair.Key;
            var exitCode = StartProcess(dotnetcli,
                new ProcessSettings
                {
                    Arguments = $"test --framework {framework} -xml {logFolder}/{project}-{framework}-result.xml -notrait category=failing",
                    WorkingDirectory = $"{testFolder}/{project}"
                });
            if (exitCode != 0)
            {
                throw new Exception($"Test failed {project} / {framework}");
            }
        }
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
            var runtime = GetRuntimeInPath($"{testFolder}/{project}/bin/{testConfiguration}/{framework}/*");
            var instanceFolder = $"{testFolder}/{project}/bin/{testConfiguration}/{framework}/{runtime}";
            // Copy xunit executable to test folder to solve path errors
            CopyFileToDirectory($"{toolsFolder}/xunit.runner.console/tools/xunit.console.exe", instanceFolder);
            CopyFileToDirectory($"{toolsFolder}/xunit.runner.console/tools/xunit.runner.utility.desktop.dll", instanceFolder);
            var logFile = $"{logFolder}/{project}-{framework}-result.xml";
            var xunitSettings = new XUnit2Settings
            {
                ToolPath = $"{instanceFolder}/xunit.console.exe",
                ArgumentCustomization = builder =>
                {
                    builder.Append("-xml");
                    builder.Append(logFile);
                    return builder;
                }
            };
            xunitSettings.ExcludeTrait("category", new[] { "failing" });
            XUnit2($"{instanceFolder}/{project}.dll", xunitSettings);
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
    foreach (var framework in buildPlan.Frameworks)
    {
        foreach (var runtime in buildPlan.Rids)
        {
            var outputFolder = $"{publishFolder}/{project}/{runtime}/{framework}";
            var publishArguments = "publish";
            if (!runtime.Equals("default"))
            {
                publishArguments = $"{publishArguments} --runtime {runtime}";
            }
            publishArguments = $"{publishArguments} --framework {framework} --configuration {configuration}";
            publishArguments = $"{publishArguments} --output {outputFolder} {sourceFolder}/{project}";
            var exitCode = StartProcess(dotnetcli,
                new ProcessSettings
                {
                    Arguments = publishArguments
                });
            if (exitCode != 0)
            {
                throw new Exception($"Failed to publish {project} / {framework}");
            }

            if (!requireArchive)
            {
                continue;
            }

            var runtimeShort = "";
            if (runtime.Equals("default"))
            {
                runtimeShort = EnvironmentVariable("OMNISHARP_PACKAGE_OSNAME");
            }
            else
            {
                // Remove version number
                runtimeShort = Regex.Replace(runtime, "(\\d|\\.)*-", "-");
            }

            var buildIdentifier = $"{runtimeShort}-{framework}";
            // Linux + net451 is renamed to Mono
            if (runtimeShort.Contains("ubuntu-") && framework.Equals("net451"))
            {
                buildIdentifier ="linux-mono";
            }
            // No need to package for <!win7> + net451
            else if (!runtimeShort.Contains("win7-") && framework.Equals("net451"))
            {
                continue;
            }

            DoArchive(runtime, outputFolder, $"{packageFolder}/{buildPlan.MainProject.ToLower()}-{buildIdentifier}");

            // Alias linux
            if (runtimeShort.Contains("ubuntu-") && !framework.Equals("net451"))
            {
                buildIdentifier = $"linux-{framework}";
                DoArchive(runtime, outputFolder, $"{packageFolder}/{buildPlan.MainProject.ToLower()}-{buildIdentifier}");
            }
        }
    }
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
    foreach (var framework in buildPlan.Frameworks)
    {
        // Skip testing mono executables
        if (!IsRunningOnWindows() && !framework.Equals("netcoreapp"))
        {
            continue;
        }
        var outputFolder = $"{publishFolder}/{project}/default/{framework}";
        var process = StartAndReturnProcess($"{outputFolder}/{project}",
            new ProcessSettings
            {
                Arguments = $"-s {sourceFolder}/{project} --stdio",
            });
        // Wait 10 seconds to see if project terminates early with error
        bool exitsWithError = process.WaitForExit(10000);
        if (exitsWithError)
        {
            throw new Exception($"Failed to run {project} / default / {framework}");
        }
    }
});

/// <summary>
///  Clean install path.
/// </summary>
Task("CleanupInstall")
    .Does(() =>
{
    if (DirectoryExists(installFolder))
    {
        CleanDirectory(installFolder);
    }
    else
    {
        CreateDirectory(installFolder);
    }
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
        var outputFolder = $"{publishFolder}/{project}/default/{framework}";
        CopyDirectory(outputFolder, $"{installFolder}/{framework}");
    }
});

/// <summary>
///  Full build targeting all RIDs specified in build.json.
/// </summary>
Task("All")
    .IsDependentOn("Cleanup")
    .IsDependentOn("Restore")
    .IsDependentOn("TestCore")
    .IsDependentOn("Test")
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
    .IsDependentOn("TestCore")
    .IsDependentOn("Test")
    .IsDependentOn("LocalPublish")
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
    var jDepVersion = JObject.Parse(System.IO.File.ReadAllText($"{workingDirectory}/depversion.json"));
    var projects = GetFiles($"{workingDirectory}/src/*/project.json");
    projects.Add(GetFiles($"{workingDirectory}/tests/*/project.json"));
    foreach (var project in projects)
    {
        var jProject = JObject.Parse(System.IO.File.ReadAllText(project.FullPath));
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
        System.IO.File.WriteAllText(project.FullPath, JsonConvert.SerializeObject(jProject, Formatting.Indented));
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
