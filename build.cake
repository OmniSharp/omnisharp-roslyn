#addin "Cake.FileHelpers"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var testConfiguration = Argument("test-configuration", "Debug");

var shell = IsRunningOnWindows() ? "powershell" : "sh";
var shellArgument = IsRunningOnWindows() ? "/Command" : "-c";
var shellExtension = IsRunningOnWindows() ? "ps1" : "sh";

string[] runtimes = IsRunningOnWindows() ? new string[] { "win7-x64", "win7-x86" } : 
                        (HasEnvironmentVariable("TRAVIS_OS_NAME")?
                            (EnvironmentVariable("TRAVIS_OS_NAME").Equals("osx")? new string[] { "darwin-x64" } :
                                new string[] { "linux-x64" }) :
                            new string[] { "" }
                        );

var dotnetFolder = "./.dotnet";
var dotnetcli = String.Format("{0}/cli/bin/dotnet", dotnetFolder);
var toolsFolder = "./tools";
var xunitRunner = "xunit.runner.console";

var artifactFolder = "./artifacts";
var publishFolder = String.Format("{0}/publish", artifactFolder);
var logFolder = String.Format("{0}/logs", artifactFolder);
var installFolder = IsRunningOnWindows() ? "./fakeinstall/local" : "~/.omnisharp/local";

string[] skipBuild = {      "OmniSharp.DotNet",
                            "OmniSharp.MSBuild",
                            "OmniSharp.NuGet",
                            "OmniSharp.ScriptCs" };
                       
string[] skipTestCore = {   "OmniSharp.MSBuild.Tests",
                            "OmniSharp.Tests" };

string[] doPublish = {      "OmniSharp" };

Task("Cleanup")
    .Does(() =>
{
    if (DirectoryExists(artifactFolder))
        CleanDirectory(artifactFolder);
    else
        CreateDirectory(artifactFolder);
    CreateDirectory(logFolder);
});

Task("BuildEnvironment")
    .Does(() =>
{
    var installScript = String.Format("install.{0}", shellExtension);
    CreateDirectory(dotnetFolder);
    var scriptPath = new FilePath(String.Format("{0}/{1}", dotnetFolder, installScript));
    //DownloadFile(String.Format("https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/{0}", installScript), scriptPath);
    var installArgs = IsRunningOnWindows() ? String.Format("beta -InstallDir {0}", dotnetFolder) :
                            String.Format("-c beta -d {0}", dotnetFolder);
    StartProcess("powershell", new ProcessSettings{ Arguments = String.Format("{0} {1} {2}", 
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

Task("BuildCore")
    .IsDependentOn("Restore")
    .Does(() =>
{
    foreach (var project in GetDirectories("./src/*"))
    {
        if(skipBuild.Contains(project.GetDirectoryName()))
            continue;
        var exitCode = StartProcess(dotnetcli, 
            new ProcessSettings{ Arguments = String.Format("build --framework dnxcore50 --configuration {0} {1}", configuration, project) });
        if (exitCode != 0)
        {
            throw new Exception(String.Format("Failed to build {0} / dnxcore50", project.GetDirectoryName()));
        }
    }
});

Task("TestBuildCore")
    //.IsDependentOn("Restore")
    .Does(() =>
{
    foreach (var project in GetDirectories("./tests/*"))
    {
        if(skipTestCore.Contains(project.GetDirectoryName()))
            continue;
        var process = StartAndReturnProcess(dotnetcli, 
            new ProcessSettings
            { 
                Arguments = String.Format("build --framework dnxcore50 --configuration {0} {1}", testConfiguration, project),
                RedirectStandardOutput = true
            });
        process.WaitForExit();
        FileWriteLines(String.Format("{0}/{1}-dnxcore50-build.log", logFolder, project.GetDirectoryName()), process.GetStandardOutput().ToArray());
    }
});

Task("TestCore")
    .IsDependentOn("TestBuildCore")
    .Does(() =>
{
    foreach (var project in GetDirectories("./tests/*"))
    {
        if(skipTestCore.Contains(project.GetDirectoryName()))
            continue;
        var exitCode = StartProcess(dotnetcli, 
            new ProcessSettings
            { 
                Arguments = String.Format("test -xml {0}/{1}-dnxcore50-result.xml -notrait category=failing", logFolder, project.GetDirectoryName()),
                WorkingDirectory = project
            });
        if (exitCode != 0)
        {
            throw new Exception(String.Format("Test failed {0} / dnxcore50", project.GetDirectoryName()));
        }
    }
});

Task("OnlyPublishCore")
    .Does(() =>
{
    foreach (var project in GetDirectories("./src/*"))
    {
        if(!doPublish.Contains(project.GetDirectoryName()))
            continue;
        foreach (var runtime in runtimes)
        {
            var runtimeOption = IsRunningOnWindows() ? String.Format("--runtime {0}", runtime) : "";
            var outputFolder = String.Format("{0}/{1}/{2}/dnxcore50", publishFolder, project.GetDirectoryName(), runtime);
            var exitCode = StartProcess(dotnetcli, 
                new ProcessSettings{ Arguments = String.Format("publish --framework dnxcore50 {0} --configuration {1} --output {2} {3}", 
                                                    runtimeOption, configuration, outputFolder, project) });
            if (exitCode != 0)
            {
                throw new Exception(String.Format("Failed to publish {0} / dnxcore50", project.GetDirectoryName()));
            }
            // Do not create archive if runtime not available (UNIX outside of Travis)
            if (runtime.Equals(""))
                continue;
            var publishedRuntime = runtime.Replace("win7-", "win-");
            Zip(outputFolder, String.Format("{0}/omnisharp-coreclr-{1}.zip", artifactFolder, publishedRuntime));
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
    .IsDependentOn("Cleanup")
    .IsDependentOn("OnlyPublishCore")
    .Does(() =>
{
});

Task("Install")
    .IsDependentOn("Cleanup")
    .IsDependentOn("OnlyPublishCore")
    .IsDependentOn("CleanupInstall")
    .Does(() =>
{
    foreach (var project in GetDirectories("./src/*"))
    {
        if(!doPublish.Contains(project.GetDirectoryName()))
            continue;
        foreach (var runtime in runtimes)
        {
            var outputFolder = String.Format("{0}/{1}/{2}", publishFolder, project.GetDirectoryName(), runtime);
            CopyDirectory(outputFolder, installFolder);
        }
    }
});

Task("Default")
    .IsDependentOn("Cleanup")
    .IsDependentOn("BuildCore")
    .IsDependentOn("TestCore")
    .IsDependentOn("OnlyPublishCore")
    .Does(() =>
{
});

RunTarget(target);