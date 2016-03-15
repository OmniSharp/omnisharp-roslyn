#addin "Cake.FileHelpers"
#addin "Cake.Json"

using System.Text.RegularExpressions;

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var testConfiguration = Argument("test-configuration", "Debug");

var environment = new CakeEnvironment();

var shell = IsRunningOnWindows() ? "powershell" : "bash";
var shellArgument = IsRunningOnWindows() ? "/Command" : "-C";
var shellExtension = IsRunningOnWindows() ? "ps1" : "sh";

public class BuildPlan
{
    public IDictionary<string, string[]> TestProjects { get; set; }
    public string BuildToolsFolder { get; set; }
    public string ArtifactsFolder { get; set; }
    public string DotNetFolder { get; set; }
    public string[] Frameworks { get; set; }
    public string[] Rids { get; set; }
    public string MainProject { get; set; }
}

var buildPlan = DeserializeJsonFromFile<BuildPlan>($"{environment.WorkingDirectory}/build.json");

var dotnetFolder = $"{environment.WorkingDirectory}/{buildPlan.DotNetFolder}";
var dotnetcli = IsRunningOnWindows() ? $"{dotnetFolder}/cli/bin/dotnet" :
                    $"{dotnetFolder}/bin/dotnet";
var toolsFolder = $"{environment.WorkingDirectory}/tools";
var xunitRunner = "xunit.runner.console";

var sourceFolder = $"{environment.WorkingDirectory}/src";
var testFolder = $"{environment.WorkingDirectory}/tests";

var artifactFolder = $"{environment.WorkingDirectory}/{buildPlan.ArtifactsFolder}";
var publishFolder = $"{artifactFolder}/publish";
var logFolder = $"{artifactFolder}/logs";
var packageFolder = $"{artifactFolder}/package";
var installFolder = IsRunningOnWindows() ? $"{environment.WorkingDirectory}/fakeinstall/local" : "~/.omnisharp/local";

string GetLocalRuntimeID()
{
    var process = StartAndReturnProcess(dotnetcli, 
        new ProcessSettings
        { 
            Arguments = "--version",
            RedirectStandardOutput = true
        });
    process.WaitForExit();
    if (process.GetExitCode() != 0)
        throw new Exception("Failed to get run dotnet --version");
    foreach (var line in process.GetStandardOutput())
    {
        if (!line.Contains("Runtime Id"))
            continue;
        var colonIndex = line.IndexOf(':');
       return line.Substring(colonIndex + 1).Trim();
    }
    throw new Exception("Failed to get default RID for system");
}

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
            var exitCode = StartProcess("7z", new ProcessSettings
            {
                Arguments = $"a {tempFile}",
                WorkingDirectory = inputFolder
            });
            if (exitCode != 0)
                throw new Exception($"Tar-ing failed for {inputFolder} {outputFile}");
            exitCode = StartProcess("7z", new ProcessSettings
            {
                Arguments = $"a {tarFile} {tempFile}",
                WorkingDirectory = inputFolder
            });
            if (exitCode != 0)
                throw new Exception($"Compression failed for {inputFolder} {outputFile}");
            DeleteFile(tempFile);
        }
        // Use tar to create TAR.GZ on Unix
        else
        {
            var exitCode =  StartProcess("tar", new ProcessSettings
            {
                Arguments = $"czf {tarFile} .",
                WorkingDirectory = inputFolder
            });
            if (exitCode != 0)
                throw new Exception($"Compression failed for {inputFolder} {outputFile}");
        }
    }
}

string GetRuntimeInPath(string path)
{
    var potentialRuntimes = GetDirectories(path);
    if (potentialRuntimes.Count != 1)
        throw new Exception($"Multiple runtimes when only one expected in {path}");
    var enumerator = potentialRuntimes.GetEnumerator();
    enumerator.MoveNext();
    return enumerator.Current.GetDirectoryName();
}

Task("RestrictToLocalRuntime")
    .Does(() =>
{
    var localRuntime = GetLocalRuntimeID();
    if (buildPlan.Rids.Contains(localRuntime))
        buildPlan.Rids = new string[] { localRuntime };
    else
    {
        foreach (var runtime in buildPlan.Rids)
        {
            var localRuntimeWithoutVersion = Regex.Replace(localRuntime, "(\\d|\\.)*-", "-");
            var runtimeWithoutVersion = Regex.Replace(runtime, "(\\d|\\.)*-", "-");
            if (localRuntimeWithoutVersion.Equals(runtimeWithoutVersion))
            {
                buildPlan.Rids = new string[] { runtime };
                return;
            }
        }
    }
    throw new Exception("Local default runtime is not in supported by configured runtimes");
});

Task("Cleanup")
    .Does(() =>
{
    if (DirectoryExists(artifactFolder))
        CleanDirectory(artifactFolder);
    else
        CreateDirectory(artifactFolder);
    CreateDirectory(logFolder);
    CreateDirectory(packageFolder);
});

Task("BuildEnvironment")
    .Does(() =>
{
    var installScript = String.Format("install.{0}", shellExtension);
    CreateDirectory(dotnetFolder);
    var scriptPath = new FilePath($"{dotnetFolder}/{installScript}");
    DownloadFile($"https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/{installScript}", scriptPath);
    if (!IsRunningOnWindows())
    {
        StartProcess("chmod", new ProcessSettings{ Arguments = String.Format("+x {0}",
            scriptPath) });
    }
    var installArgs = IsRunningOnWindows() ? String.Format("beta -InstallDir {0}", dotnetFolder) :
                            String.Format("-c beta -d {0}", dotnetFolder);
    StartProcess(shell, new ProcessSettings{ Arguments = String.Format("{0} {1} {2}", 
            shellArgument, scriptPath, installArgs) });
    CreateDirectory(toolsFolder);
    if (!FileExists(String.Format("{0}/{1}", toolsFolder, xunitRunner)))
    {
        NuGetInstall(xunitRunner, new NuGetInstallSettings {
            ExcludeVersion  = true,
            OutputDirectory = toolsFolder,
            NoCache = true,
            Prerelease = true
        });
    }
});

Task("Restore")
    .IsDependentOn("BuildEnvironment")
    .Does(() =>
{
    var exitCode = StartProcess(dotnetcli, 
        new ProcessSettings{ Arguments = "restore" });
    if (exitCode != 0)
    {
        throw new Exception("Failed to restore.");
    }
});

Task("TestBuild")
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

Task("TestCore")
    .IsDependentOn("TestBuild")
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

Task("Test")
    .IsDependentOn("TestBuild")
    .Does(() =>
{
    foreach (var pair in buildPlan.TestProjects)
    {
        foreach (var framework in pair.Value)
        {
            if (framework.Equals("dnxcore50"))
            {
                continue;
            }

            var project = pair.Key;
            var runtime = GetRuntimeInPath($"{testFolder}/{project}/bin/{testConfiguration}/{framework}/*");
            var instanceFolder = $"{testFolder}/{project}/bin/{testConfiguration}/{framework}/{runtime}";
            CopyFileToDirectory($"{environment.WorkingDirectory}/tools/xunit.runner.console/tools/xunit.console.exe", instanceFolder);
            CopyFileToDirectory($"{environment.WorkingDirectory}/tools/xunit.runner.console/tools/xunit.runner.utility.desktop.dll", instanceFolder);
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

Task("OnlyPublish")
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
                buildIdentifier ="linux-mono";
            // No need to package OSX + dnx451
            else if (runtimeShort.Contains("osx-") && framework.Equals("dnx451"))
                continue;
            
            DoArchive(runtime, outputFolder, $"{packageFolder}/{buildPlan.MainProject.ToLower()}-{buildIdentifier}");
        }
    }
});

Task("Publish")
    .IsDependentOn("Restore")
    .IsDependentOn("OnlyPublish")
    .Does(() =>
{
});

Task("TestPublish")
    .IsDependentOn("Publish")
    .IsDependentOn("RestrictToLocalRuntime")
    .Does(() =>
{
    var project = buildPlan.MainProject;
    foreach (var framework in buildPlan.Frameworks)
    {
        foreach (var runtime in buildPlan.Rids)
        {
            var outputFolder = $"{publishFolder}/{project}/{runtime}/{framework}";
            var process = StartAndReturnProcess($"{outputFolder}/{project}", 
                new ProcessSettings
                { 
                    Arguments = $"-s {sourceFolder}/{project}",
                });
            bool exitsWithError = process.WaitForExit(10000);
            if (exitsWithError)
                throw new Exception($"Could not run {project} on {runtime}-{framework}");
        }
    }
});

Task("CleanupInstall")
    .Does(() =>
{
    if (DirectoryExists(installFolder))
        CleanDirectory(installFolder);
    else
        CreateDirectory(installFolder);
});

Task("Quick")
    .IsDependentOn("RestrictToLocalRuntime")
    .IsDependentOn("Cleanup")
    .IsDependentOn("OnlyPublish")
    .Does(() =>
{
});

Task("Install")
    .IsDependentOn("RestrictToLocalRuntime")
    .IsDependentOn("Cleanup")
    .IsDependentOn("OnlyPublish")
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

Task("Local")
    .IsDependentOn("RestrictToLocalRuntime")
    .IsDependentOn("Default")
    .Does(() =>
{
});

Task("Default")
    .IsDependentOn("Cleanup")
    .IsDependentOn("TestCore")
    .IsDependentOn("Test")
    .IsDependentOn("Publish")
    .IsDependentOn("TestPublish")
    .Does(() =>
{
});

RunTarget(target);
