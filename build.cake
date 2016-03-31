#addin "Cake.FileHelpers"
#addin "Cake.Json"

using System.Text.RegularExpressions;
using System.ComponentModel;

// Basic arguments
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
// Optional arguments
var testConfiguration = Argument("test-configuration", "Debug");
var installFolder = Argument("install-path", IsRunningOnWindows() ?
                        $"{EnvironmentVariable("USERPROFILE")}/.omnisharp/local" : "~/.omnisharp/local");

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

var buildPlan = DeserializeJsonFromFile<BuildPlan>($"{workingDirectory}/build.json");

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
///  Retrieve the default local RID from .NET CLI.
/// </summary>
/// <param name="dotnetcli">Full path to .NET CLI binary</param>
/// <returns>Default RID</returns>
string GetLocalRuntimeID(string dotnetcli)
{
    var process = StartAndReturnProcess(dotnetcli,
        new ProcessSettings
        {
            // Soon to be --info
            // Arguments = "--info",
            Arguments = "--version",
            RedirectStandardOutput = true
        });
    process.WaitForExit();
    foreach (var line in process.GetStandardOutput())
    {
        // Soon to be RID
        // if (!line.Contains("RID"))
        if (!line.Contains("Runtime Id"))
        {
            continue;
        }
        var colonIndex = line.IndexOf(':');
        return line.Substring(colonIndex + 1).Trim();
    }
    throw new Exception("Failed to get default RID for system");
}

/// <summary>
///  Match the local RID with the ones specified in build.json.
///  Return exact match if found.
///  Return first OS match (without version number) otherwise.
///  Report error if no OS match found.
/// </summary>
/// <param name="dotnetcli">Full path to .NET CLI binary</param>
/// <param name="buildPlan">BuildPlan from build.json</param>
/// <returns>Matched RID</returns>
string MatchLocalRuntimeID(string dotnetcli, BuildPlan buildPlan)
{
    var localRuntime = GetLocalRuntimeID(dotnetcli);
    if (buildPlan.Rids.Contains(localRuntime))
    {
        return localRuntime;
    }
    else
    {
        return buildPlan.Rids.First(runtime => {
            var localRuntimeWithoutVersion = Regex.Replace(localRuntime, "(\\d|\\.)*?-", "-");
            var runtimeWithoutVersion = Regex.Replace(runtime, "(\\d|\\.)*?-", "-");
            return localRuntimeWithoutVersion.Equals(runtimeWithoutVersion);
        });
    }
}

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
    if (runtime.Contains("win"))
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
    var potentialRuntimes = GetDirectories(path);
    if (potentialRuntimes.Count != 1)
    {
        throw new Exception($"Multiple runtimes when only one expected in {path}");
    }
    var enumerator = potentialRuntimes.GetEnumerator();
    enumerator.MoveNext();
    return enumerator.Current.GetDirectoryName();
}

/// <summary>
///  Restrict the RIDs defined in build.json to a RID matching the local one.
/// </summary>
Task("RestrictToLocalRuntime")
    .IsDependentOn("Setup")
    .Does(() =>
{
    buildPlan.Rids = new string[] { MatchLocalRuntimeID(dotnetcli, buildPlan) };
});

/// <summary>
///  Restrict the RIDs for the specific environment
/// </summary>
Task("RestrictToEnvironmentRuntimes")
    .IsDependentOn("BuildEnvironment")
    .Does(() =>
{
    // Limit scope if things we build
    buildPlan.Rids = buildPlan.Rids.Where(runtime => {
        if (IsRunningOnWindows())
        {
            return runtime.StartsWith("win");
        }
        else
        {
            var localRuntime = GetLocalRuntimeID(dotnetcli);
            var localRuntimeWithoutVersion = Regex.Replace(localRuntime, "(\\d|\\.)*-", "-");
            var runtimeWithoutVersion = Regex.Replace(runtime, "(\\d|\\.)*-", "-");
            return localRuntimeWithoutVersion.Equals(runtimeWithoutVersion);
        }
    }).ToArray();
});



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
///  Pre-build setup tasks
/// </summary>
Task("Setup")
    .IsDependentOn("BuildEnvironment")
    .IsDependentOn("RestrictToEnvironmentRuntimes")
    .Does(() =>
{
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
    string installArgs = "";

    if (IsRunningOnWindows())
    {
        installArgs = $"{buildPlan.DotNetChannel}";
        if (!String.IsNullOrEmpty(buildPlan.DotNetVersion))
            installArgs = $"{installArgs} -version {buildPlan.DotNetVersion}";
        if (!buildPlan.UseSystemDotNetPath)
            installArgs = $"{installArgs} -InstallDir {dotnetFolder}";
    }
    else
    {
        StartProcess("chmod",
            new ProcessSettings
            {
                Arguments = $"+x {scriptPath}"
            });

        installArgs = $"-c {buildPlan.DotNetChannel}";
        if (!String.IsNullOrEmpty(buildPlan.DotNetVersion))
            installArgs = $"{installArgs} -v {buildPlan.DotNetVersion}";
        if (!buildPlan.UseSystemDotNetPath)
            installArgs = $"{installArgs} -i {dotnetFolder}";
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
                Arguments = "--version"
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
            FileWriteLines($"{logFolder}/{project}-{framework}-build.log", process.GetStandardOutput().ToArray());
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
            if (!framework.Equals("dnxcore50"))
            {
                continue;
            }

            var project = pair.Key;
            var exitCode = StartProcess(dotnetcli,
                new ProcessSettings
                {
                    Arguments = $"test -xml {logFolder}/{project}-{framework}-result.xml -notrait category=failing",
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
            if (framework.Equals("dnxcore50"))
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
            var exitCode = StartProcess(dotnetcli,
                new ProcessSettings
                {
                    Arguments = $"publish --framework {framework} --runtime {runtime} " +
                                    $"--configuration {configuration} --output {outputFolder} " +
                                    $"{sourceFolder}/{project}"
                });
            if (exitCode != 0)
            {
                throw new Exception($"Failed to publish {project} / {framework}");
            }

            // Remove version number on Windows
            var runtimeShort = Regex.Replace(runtime, "(\\d|\\.)*-", "-");
            // Simplify Ubuntu to Linux
            runtimeShort = runtimeShort.Replace("ubuntu", "linux");
            var buildIdentifier = $"{runtimeShort}-{framework}";
            // Linux + dnx451 is renamed to Mono
            if (runtimeShort.Contains("linux-") && framework.Equals("dnx451"))
            {
                buildIdentifier ="linux-mono";
            }
            // No need to package OSX + dnx451
            else if (runtimeShort.Contains("osx-") && framework.Equals("dnx451"))
            {
                continue;
            }

            DoArchive(runtime, outputFolder, $"{packageFolder}/{buildPlan.MainProject.ToLower()}-{buildIdentifier}");
        }
    }
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
        if (!IsRunningOnWindows() && !framework.Equals("dnxcore50"))
        {
            continue;
        }
        var runtime = MatchLocalRuntimeID(dotnetcli, buildPlan);
        var outputFolder = $"{publishFolder}/{project}/{runtime}/{framework}";
        var process = StartAndReturnProcess($"{outputFolder}/{project}",
            new ProcessSettings
            {
                Arguments = $"-s {sourceFolder}/{project} --stdio",
            });
        // Wait 10 seconds to see if project terminates early with error
        bool exitsWithError = process.WaitForExit(10000);
        if (exitsWithError)
        {
            throw new Exception($"Failed to run {project} / {runtime} / {framework}");
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
        var outputFolder = $"{publishFolder}/{project}/{buildPlan.Rids[0]}/{framework}";
        CopyDirectory(outputFolder, installFolder);
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


Task("Default")
    .IsDependentOn("Local")
    .Does(() =>
{
});

/// <summary>
///  Default to Local.
/// </summary>
RunTarget(target);
