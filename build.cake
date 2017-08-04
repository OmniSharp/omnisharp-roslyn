#load "scripts/common.cake"
#load "scripts/runhelpers.cake"
#load "scripts/archiving.cake"
#load "scripts/artifacts.cake"
#load "scripts/msbuild.cake"
#load "scripts/platform.cake"
#load "scripts/validation.cake"

using System.ComponentModel;
using System.Net;

// Arguments
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var testConfiguration = Argument("test-configuration", "Debug");
var installFolder = Argument("install-path",
    CombinePaths(Environment.GetEnvironmentVariable(Platform.Current.IsWindows ? "USERPROFILE" : "HOME"), ".omnisharp", "local"));
var requireArchive = HasArgument("archive");
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
    .IsDependentOn("BuildEnvironment")
    .IsDependentOn("PopulateRuntimes")
    .IsDependentOn("SetupMSBuild");

/// <summary>
/// Acquire additional NuGet packages included with OmniSharp (such as MSBuild).
/// </summary>
Task("SetupMSBuild")
    .IsDependentOn("BuildEnvironment")
    .Does(() =>
{
    SetupMSBuild(env, buildPlan);
});

/// <summary>
///  Populate the RIDs for the specific environment.
///  Use default RID (+ win7-x86 on Windows) for now.
/// </summary>
Task("PopulateRuntimes")
    .IsDependentOn("BuildEnvironment")
    .Does(() =>
{
    if (Platform.Current.IsWindows && string.Equals(Environment.GetEnvironmentVariable("APPVEYOR"), "True"))
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
            "ubuntu.16.10-x64",
            "centos.7-x64",
            "rhel.7.2-x64",
            "debian.8-x64",
            "fedora.23-x64",
            "fedora.24-x64",
            "opensuse.13.2-x64",
            "opensuse.42.1-x64");
    }
    else
    {
        // In this case, the build is not happening in CI, so just use the default RID.
        buildPlan.SetTargetRids("default");
    }
});

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

    buildPlan.SetCurrentRid(rid);

    Information("Using .NET CLI");
    Information("  Version: {0}", version);
    Information("  RID: {0}", rid);
    Information("  Base Path: {0}", basePath);
});

Task("ValidateEnvironment")
    .Does(() =>
{
    if (!Platform.Current.IsWindows)
    {
        ValidateMonoVersion(buildPlan);
    }
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
///  Install/update build environment.
/// </summary>
Task("BuildEnvironment")
    .IsDependentOn("ValidateEnvironment")
    .IsDependentOn("InstallDotNetCoreSdk")
    .IsDependentOn("InstallMonoAssets")
    .Does(() =>
{
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
    var command = Platform.Current.IsWindows
        ? env.DotNetCommand
        : env.ShellCommand;

    var arguments = Platform.Current.IsWindows
        ? $"build \"{projectFilePath}\" --configuration {configuration} /v:d"
        : $"{env.ShellArgument} msbuild \"{projectFilePath}\" /p:Configuration={configuration} /v:d";

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
///  Run all tests for .NET Desktop and .NET Core
/// </summary>
Task("TestAll")
    .IsDependentOn("Test")
    .Does(() =>{});

/// <summary>
///  Run tests for other frameworks (using XUnit2).
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

bool IsNetFrameworkOnUnix(string framework)
{
    return !Platform.Current.IsWindows
        && !framework.StartsWith("netcore")
        && !framework.StartsWith("netstandard");
}

string GetPublishArguments(string projectFileName, string rid, string framework, string configuration, string outputFolder)
{
    var argList = new List<string>();

    if (IsNetFrameworkOnUnix(framework))
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
        argList.Add($"--runtime {rid}");
        argList.Add($"--framework {framework}");
        argList.Add($"--configuration {configuration}");
        argList.Add($"--output \"{outputFolder}\"");
    }

    return string.Join(" ", argList);
}

void CopyMonoBuild(BuildEnvironment env, string sourceFolder, string outputFolder)
{
    DirectoryHelper.Copy(sourceFolder, outputFolder, copySubDirectories: false);

    // Copy MSBuild runtime and libraries
    DirectoryHelper.Copy($"{env.Folders.MSBuildBase}-net46", CombinePaths(outputFolder, "msbuild"));
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

void PublishMonoBuild(BuildEnvironment env, BuildPlan plan, string configuration, bool archive)
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
}

void PublishMonoBuildForPlatform(MonoRuntime monoRuntime, BuildEnvironment env, BuildPlan plan, bool archive)
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
}

Task("PublishMonoBuilds")
    .IsDependentOn("Setup")
    .WithCriteria(() => !Platform.Current.IsWindows)
    .Does(() =>
{
    PublishMonoBuild(env, buildPlan, configuration, requireArchive);

    foreach (var monoRuntime in env.MonoRuntimes)
    {
        PublishMonoBuildForPlatform(monoRuntime, env, buildPlan, requireArchive);
    }
});

void PublishWindowsBuild(BuildEnvironment env, string configuration, string rid, bool archive)
{
    var project = buildPlan.MainProject;
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
    DirectoryHelper.Copy($"{env.Folders.MSBuildBase}-net46", CombinePaths(outputFolder, "msbuild"));

    if (archive)
    {
        Package(rid, outputFolder, env.Folders.ArtifactsPackage);
    }
}

Task("PublishWindowsBuilds")
    .IsDependentOn("Setup")
    .WithCriteria(() => Platform.Current.IsWindows)
    .Does(() =>
{
    PublishWindowsBuild(env, configuration, "win7-x86", requireArchive);
    PublishWindowsBuild(env, configuration, "win7-x64", requireArchive);
});

Task("Publish")
    .IsDependentOn("PublishMonoBuilds")
    .IsDependentOn("PublishWindowsBuilds");

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

    var completed = new HashSet<string>();

    foreach (var runtime in buildPlan.TargetRids)
    {
        if (completed.Contains(runtime))
        {
            continue;
        }

        var rid = runtime.Equals("default")
            ? buildPlan.GetDefaultRid()
            : runtime;

        // Restore the OmniSharp.csproj with this runtime.
        PrintBlankLine();
        var runtimeText = runtime;
        if (runtimeText.Equals("default"))
        {
            runtimeText += " (" + rid + ")";
        }

        Information($"Restoring packages in {projectName} for {runtimeText}...");

        RunTool(env.DotNetCommand, $"restore \"{projectFileName}\" --runtime {rid}", env.WorkingDirectory)
            .ExceptionOnError($"Failed to restore {projectName} for {rid}.");

        foreach (var framework in buildPlan.Frameworks)
        {
            var outputFolder = CombinePaths(env.Folders.ArtifactsPublish, project, runtime, framework);

            var command = IsNetFrameworkOnUnix(framework)
                ? env.ShellCommand
                : env.DotNetCommand;

            var args = GetPublishArguments(projectFileName, rid, framework, configuration, outputFolder);

            args = IsNetFrameworkOnUnix(framework)
                ? $"{env.ShellArgument} msbuild {args}"
                : args;

            Information("Publishing {0} for {1}/{2}...", projectName, framework, rid);

            RunTool(command, args, env.WorkingDirectory)
                .ExceptionOnError($"Failed to publish {project} for {framework}/{rid}");

            // Copy MSBuild and SDKs to output
            DirectoryHelper.Copy($"{env.Folders.MSBuildBase}-{framework}", CombinePaths(outputFolder, "msbuild"));

            // For OSX/Linux net46 builds, copy the MSBuild libraries built for Mono.
            // In addition, delete System.Runtime.InteropServices.RuntimeInformation, which is Windows-specific.
            if (!Platform.Current.IsWindows)
            {
                DirectoryHelper.Copy($"{env.Folders.MonoMSBuildLib}", outputFolder);

                FileHelper.Delete(CombinePaths(outputFolder, "System.Runtime.InteropServices.RuntimeInformation.dll"));
            }

            if (requireArchive)
            {
                Package(runtime, outputFolder, env.Folders.ArtifactsPackage);
            }
        }

        completed.Add(runtime);
    }

    CreateRunScript(CombinePaths(env.Folders.ArtifactsPublish, project, "default"), env.Folders.ArtifactsScripts);
});

/// <summary>
///  Alias for OnlyPublish.
///  Targets all RIDs as specified in build.json.
/// </summary>
Task("AllPublish")
    .IsDependentOn("Restore")
    .IsDependentOn("OnlyPublish"));

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
    .IsDependentOn("OnlyPublish");

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
    var scriptsToTest = new string[] {"OmniSharp"};
    foreach (var script in scriptsToTest)
    {
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
    .IsDependentOn("LocalPublish");

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
    .IsDependentOn("TestPublished");

/// <summary>
///  Full build targeting local RID.
/// </summary>
Task("Local")
    .IsDependentOn("Cleanup")
    .IsDependentOn("Restore")
    .IsDependentOn("TestAll")
    .IsDependentOn("LocalPublish")
    .IsDependentOn("TestPublished");

/// <summary>
///  Default Task aliases to Local.
/// </summary>
Task("Default")
    .IsDependentOn("Local");

/// <summary>
///  Default to Local.
/// </summary>
RunTarget(target);
