#addin "Cake.FileHelpers"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var testConfiguration = Argument("test-configuration", "Debug");

var shell = IsRunningOnWindows() ? "powershell" : "bash";
var shellArgument = IsRunningOnWindows() ? "/Command" : "-C";
var shellExtension = IsRunningOnWindows() ? "ps1" : "sh";

string[] runtimes = { "win7-x64", "win7-x86" };
// Cannot use ternary operator on Mono with array initializer
if (!IsRunningOnWindows())
{
    if (EnvironmentVariable("OSSTRING").Equals("osx"))
        runtimes = new string[] { "osx.10.10" };
    else
        runtimes = new string[] { "ubuntu.14.04-x64" };
}

var dotnetFolder = "./.dotnet";
var dotnetcli = IsRunningOnWindows() ? String.Format("{0}/cli/bin/dotnet", dotnetFolder) :
                    String.Format("{0}/bin/dotnet", dotnetFolder);
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

string[] skipTestNet4 = {   "OmniSharp.Roslyn.CSharp.Tests",
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
    DownloadFile(String.Format("https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/{0}", installScript), scriptPath);
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
    .IsDependentOn("Restore")
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
            var runtimeOption = String.Format("--runtime {0}", runtime);
            var outputFolder = String.Format("{0}/{1}/{2}/dnxcore50", publishFolder, project.GetDirectoryName(), runtime);
            var exitCode = StartProcess(dotnetcli, 
                new ProcessSettings{ Arguments = String.Format("publish --framework dnxcore50 {0} --configuration {1} --output {2} {3}", 
                                                    runtimeOption, configuration, outputFolder, project) });
            if (exitCode != 0)
            {
                throw new Exception(String.Format("Failed to publish {0} / dnxcore50", project.GetDirectoryName()));
            }
            var publishedRuntime = runtime.Replace("win7-", "win-");
            publishedRuntime = publishedRuntime.Replace("ubuntu.14.04-", "linux-");
            publishedRuntime = publishedRuntime.Replace("osx.10.10", "darwin-x4");
            Zip(outputFolder, String.Format("{0}/omnisharp-coreclr-{1}.zip", artifactFolder, publishedRuntime));
        }
    }
});

Task("BuildNet4")
    .IsDependentOn("Restore")
    .Does(() =>
{
    foreach (var project in GetDirectories("./src/*"))
    {
        if(skipBuild.Contains(project.GetDirectoryName()))
            continue;
        var exitCode = StartProcess(dotnetcli, 
            new ProcessSettings{ Arguments = String.Format("build --framework dnx451 --configuration {0} {1}", configuration, project) });
        if (exitCode != 0)
        {
            throw new Exception(String.Format("Failed to build {0} / dnx451", project.GetDirectoryName()));
        }
    }
});

Task("TestBuildNet4")
    .IsDependentOn("Restore")
    .Does(() =>
{
    foreach (var project in GetDirectories("./tests/*"))
    {
        if(skipTestNet4.Contains(project.GetDirectoryName()))
            continue;
        var process = StartAndReturnProcess(dotnetcli, 
            new ProcessSettings
            { 
                Arguments = String.Format("build --framework dnx451 --configuration {0} {1}", testConfiguration, project),
                RedirectStandardOutput = true
            });
        process.WaitForExit();
        FileWriteLines(String.Format("{0}/{1}-dnx451-build.log", logFolder, project.GetDirectoryName()), process.GetStandardOutput().ToArray());
    }
});

Task("TestNet4")
    .IsDependentOn("TestBuildNet4")
    .Does(() =>
{
    foreach (var project in GetDirectories("./tests/*"))
    {
        if(skipTestNet4.Contains(project.GetDirectoryName()))
            continue;
        XUnit(String.Format("{0}/bin/{1}/dnx451/", project.FullPath, testConfiguration),
                new XUnitSettings
                { 
                    ArgumentCustomization = builder =>
                    {
                        var xunitArgs = new ProcessArgumentBuilder().Append("-notrait");
                        xunitArgs.Append("category=failing");
                        xunitArgs.Append("-xml");
                        xunitArgs.Append(String.Format("{0}/{1}-dnx451-result.xml", logFolder, project.GetDirectoryName()));
                        return xunitArgs;
                    }
                });
    }
});

Task("OnlyPublishNet4")
    .Does(() =>
{
    foreach (var project in GetDirectories("./src/*"))
    {
        if(!doPublish.Contains(project.GetDirectoryName()))
            continue;
        foreach (var runtime in runtimes)
        {
            var runtimeOption = String.Format("--runtime {0}", runtime);
            var outputFolder = String.Format("{0}/{1}/{2}/dnx451", publishFolder, project.GetDirectoryName(), runtime);
            var exitCode = StartProcess(dotnetcli, 
                new ProcessSettings{ Arguments = String.Format("publish --framework dnx451 {0} --configuration {1} --output {2} {3}", 
                                                    runtimeOption, configuration, outputFolder, project) });
            if (exitCode != 0)
            {
                throw new Exception(String.Format("Failed to publish {0} / dnx451", project.GetDirectoryName()));
            }
            // Copy binding redirect configuration respectively to mitigate dotnet publish bug
            CopyFile(String.Format("{0}/bin/{1}/dnx451/{2}/{3}.exe.config", project.FullPath, configuration, runtime, project.GetDirectoryName()),
                outputFolder);
            var publishedRuntime = runtime.Replace("win7-", "win-");
            publishedRuntime = publishedRuntime.Replace("ubuntu.14.04-", "linux-");
            publishedRuntime = publishedRuntime.Replace("osx.10.10", "darwin-x4");
            if (IsRunningOnWindows())
                Zip(outputFolder, String.Format("{0}/omnisharp-clr-{1}.zip", artifactFolder, publishedRuntime));
            else
                Zip(outputFolder, String.Format("{0}/omnisharp-mono.zip", artifactFolder));
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
    .IsDependentOn("OnlyPublishNet4")
    .Does(() =>
{
});

Task("Install")
    .IsDependentOn("Cleanup")
    .IsDependentOn("OnlyPublishCore")
    .IsDependentOn("OnlyPublishNet4")
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
    .IsDependentOn("BuildNet4")
    .IsDependentOn("TestNet4")
    .IsDependentOn("OnlyPublishNet4")
    .Does(() =>
{
});

RunTarget(target);
