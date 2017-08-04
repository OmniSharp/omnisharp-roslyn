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

Information("Current platform: {0}", Platform.Current);

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

void ParseDotNetInfoValues(IEnumerable<string> lines, out string version, out string rid, out string basePath)
{
    version = null;
    rid = null;
    basePath = null;

    foreach (var line in lines)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex >= 0)
        {
            var name = line.Substring(0, colonIndex).Trim();
            var value = line.Substring(colonIndex + 1).Trim();

            if (string.IsNullOrWhiteSpace(version) && name.Equals("Version", StringComparison.OrdinalIgnoreCase))
            {
                version = value;
            }
            else if (string.IsNullOrWhiteSpace(rid) && name.Equals("RID", StringComparison.OrdinalIgnoreCase))
            {
                rid = value;
            }
            else if (string.IsNullOrWhiteSpace(basePath) && name.Equals("Base Path", StringComparison.OrdinalIgnoreCase))
            {
                basePath = value;
            }
        }
    }

    if (string.IsNullOrWhiteSpace(version))
    {
        throw new Exception("Could not locate Version in 'dotnet --info' output.");
    }

    if (string.IsNullOrWhiteSpace(rid))
    {
        throw new Exception("Could not locate RID in 'dotnet --info' output.");
    }

    if (string.IsNullOrWhiteSpace(basePath))
    {
        throw new Exception("Could not locate Base Path in 'dotnet --info' output.");
    }
}

void InstallDotNetSdk(BuildEnvironment env, BuildPlan plan, string version, string installFolder)
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
    }

    // Install legacy .NET Core SDK (used to 'dotnet restore' project.json test projects)
    InstallDotNetSdk(env, buildPlan,
        version: buildPlan.LegacyDotNetVersion,
        installFolder: env.Folders.LegacyDotNetSdk);

    string DOTNET_CLI_UI_LANGUAGE = "DOTNET_CLI_UI_LANGUAGE";
    var originalUILanguageValue = Environment.GetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE);
    Environment.SetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE, "en-US");

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
    finally
    {
        Environment.SetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE, originalUILanguageValue);
    }

    string version, rid, basePath;
    ParseDotNetInfoValues(lines, out version, out rid, out basePath);

    if (rid == "osx.10.12-x64")
    {
        rid = "osx.10.11-x64";
        Environment.SetEnvironmentVariable("DOTNET_RUNTIME_ID", rid);
    }

    Information("Using .NET CLI");
    Information("  Version: {0}", version);
    Information("  RID: {0}", rid);
    Information("  Base Path: {0}", basePath);
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

/// <summary>
/// Create '.msbuild' folder and copy content to it.
/// </summary>
Task("CreateMSBuildFolder")
    .IsDependentOn("InstallMonoAssets")
    .Does(() =>
{
    DirectoryHelper.ForceCreate(env.Folders.MSBuild);

    if (!Platform.Current.IsWindows)
    {
        Information("Copying Mono MSBuild runtime...");
        DirectoryHelper.Copy(env.Folders.MonoMSBuildRuntime, env.Folders.MSBuild);
    }
    else
    {
        Information("Copying MSBuild runtime...");

        var msbuildRuntimeFolder = CombinePaths(env.Folders.Tools, "Microsoft.Build.Runtime", "contentFiles", "any", "net46");
        DirectoryHelper.Copy(msbuildRuntimeFolder, env.Folders.MSBuild);
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
            .ExceptionOnError($"Failed to restore '{folder}'.");
    }

    // Restore and build legacy test assets with legacy .NET Core SDK
    foreach (var project in buildPlan.LegacyTestAssets)
    {
        Information("Restoring and building project.json: {0}...", project);

        var folder = CombinePaths(env.Folders.TestAssets, "test-projects", project);

        RunTool(env.LegacyDotNetCommand, "restore", folder)
            .ExceptionOnError($"Failed to restore '{folder}'.");

        RunTool(env.LegacyDotNetCommand, $"build", folder)
            .ExceptionOnError($"Failed to restore '{folder}'.");
    }
});

void BuildProject(BuildEnvironment env, string projectName, string projectFilePath, string configuration)
{
    string command, arguments;

    // On Windows, we build exclusively with the .NET CLI.
    // On OSX/Linux, we build with the MSBuild installed with Mono.
    if (Platform.Current.IsWindows)
    {
        command = env.DotNetCommand;
        arguments = $"build \"{projectFilePath}\" --configuration {configuration} /v:d";
    }
    else
    {
        command = env.ShellCommand;
        arguments = $"{env.ShellArgument} msbuild \"{projectFilePath}\" /p:Configuration={configuration} /v:d";
    }

    var logFileName = CombinePaths(env.Folders.ArtifactsLogs, $"{projectName}-build.log");

    Information("Building {0}...", projectName);

    RunTool(command, arguments, env.WorkingDirectory, logFileName)
        .ExceptionOnError($"Building {projectName} failed.");
}

Task("BuildMain")
    .IsDependentOn("Setup")
    .IsDependentOn("Restore")
    .Does(() =>
{
    var projectName = buildPlan.MainProject + ".csproj";
    var projectFilePath = CombinePaths(env.Folders.Source, buildPlan.MainProject, projectName);

    BuildProject(env, projectName, projectFilePath, configuration);
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
    foreach (var testProject in buildPlan.TestProjects)
    {
        PrintBlankLine();

        var instanceFolder = CombinePaths(env.Folders.Tests, testProject, "bin", testConfiguration, "net46");

        // Copy xunit executable to test folder to solve path errors
        var xunitToolsFolder = CombinePaths(env.Folders.Tools, "xunit.runner.console", "tools", "net452");
        var xunitInstancePath = CombinePaths(instanceFolder, "xunit.console.exe");
        FileHelper.Copy(CombinePaths(xunitToolsFolder, "xunit.console.exe"), xunitInstancePath, overwrite: true);
        FileHelper.Copy(CombinePaths(xunitToolsFolder, "xunit.runner.utility.net452.dll"), CombinePaths(instanceFolder, "xunit.runner.utility.net452.dll"), overwrite: true);
        var targetPath = CombinePaths(instanceFolder, $"{testProject}.dll");
        var logFile = CombinePaths(env.Folders.ArtifactsLogs, $"{testProject}-desktop-result.xml");
        var arguments = $"\"{targetPath}\" -parallel none -noshadow -xml \"{logFile}\" -notrait category=failing";

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

string PublishMonoBuild(BuildEnvironment env, BuildPlan plan, string configuration, bool archive)
{
    Information("Publishing Mono build...");

    var project = plan.MainProject;
    var outputFolder = CombinePaths(env.Folders.ArtifactsPublish, project, "mono");

    var buildFolder = CombinePaths(env.Folders.Source, project, "bin", configuration, "net46");

    CopyMonoBuild(env, buildFolder, outputFolder);

    if (archive)
    {
        Package("mono", outputFolder, env.Folders.ArtifactsPackage);
    }

    return outputFolder;
}

string PublishMonoBuildForPlatform(MonoRuntime monoRuntime, BuildEnvironment env, BuildPlan plan, bool archive)
{
    Information("Publishing platform-specific Mono build: {0}", monoRuntime.PlatformName);

    var project = plan.MainProject;
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
        Package(monoRuntime.PlatformName, outputFolder, env.Folders.ArtifactsPackage);
    }

    return outputFolder;
}

Task("PublishMonoBuilds")
    .IsDependentOn("Setup")
    .WithCriteria(() => !Platform.Current.IsWindows)
    .Does(() =>
{
    var outputFolder = PublishMonoBuild(env, buildPlan, configuration, requireArchive);

    CreateRunScript(outputFolder, env.Folders.ArtifactsScripts);

    if (publishAll)
    {
        foreach (var monoRuntime in env.MonoRuntimes)
        {
            PublishMonoBuildForPlatform(monoRuntime, env, buildPlan, requireArchive);
        }
    }
});

string PublishWindowsBuild(BuildEnvironment env, BuildPlan plan, string configuration, string rid, bool archive)
{
    var project = plan.MainProject;
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
        Package(rid, outputFolder, env.Folders.ArtifactsPackage);
    }

    return outputFolder;
}

Task("PublishWindowsBuilds")
    .IsDependentOn("Setup")
    .WithCriteria(() => Platform.Current.IsWindows)
    .Does(() =>
{
    string outputFolder;

    if (publishAll)
    {
        var outputFolder32 = PublishWindowsBuild(env, buildPlan, configuration, "win7-x86", requireArchive);
        var outputFolder64 = PublishWindowsBuild(env, buildPlan, configuration, "win7-x64", requireArchive);

        outputFolder = Platform.Current.Is32Bit
            ? outputFolder32
            : outputFolder64;
    }
    else if (Platform.Current.Is32Bit)
    {
        outputFolder = PublishWindowsBuild(env, buildPlan, configuration, "win7-x86", requireArchive);
    }
    else
    {
        outputFolder = PublishWindowsBuild(env, buildPlan, configuration, "win7-x64", requireArchive);
    }

    CreateRunScript(outputFolder, env.Folders.ArtifactsScripts);
});

Task("Publish")
    .IsDependentOn("PublishMonoBuilds")
    .IsDependentOn("PublishWindowsBuilds");

/// <summary>
///  Execute the run script.
/// </summary>
Task("ExecuteRunScript")
    .Does(() =>
{
    var project = buildPlan.MainProject;
    var projectFolder = CombinePaths(env.Folders.Source, project);
    var scriptPath = CombinePaths(env.Folders.ArtifactsScripts, "OmniSharp");
    var didNotExitWithError = Run(env.ShellCommand, $"{env.ShellArgument} \"{scriptPath}\" -s \"{projectFolder}\" --stdio",
                                new RunOptions(timeOut: 30000))
                            .DidTimeOut;
                            
    if (!didNotExitWithError)
    {
        throw new Exception("Failed to run OmniSharp script");
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
    var project = buildPlan.MainProject;

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

    CreateRunScript(installFolder, env.Folders.ArtifactsScripts);

    Information($"OmniSharp is installed locally at {installFolder}");
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
