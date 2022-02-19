#load "scripts/common.cake"
#load "scripts/runhelpers.cake"
#load "scripts/archiving.cake"
#load "scripts/artifacts.cake"
#load "scripts/platform.cake"
#load "scripts/validation.cake"

using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Xml;

// Arguments
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");
var installFolder = Argument("install-path",
    CombinePaths(Environment.GetEnvironmentVariable(Platform.Current.IsWindows ? "USERPROFILE" : "HOME"), ".omnisharp"));
var publishAll = HasArgument("publish-all");
var useGlobalDotNetSdk = HasArgument("use-global-dotnet-sdk");
var testProjectArgument = Argument("test-project", "");
var useDotNetTest = HasArgument("use-dotnet-test");

Log.Context = Context;

var env = new BuildEnvironment(useGlobalDotNetSdk, Context);
var buildPlan = BuildPlan.Load(env);

Information("");
Information("Current platform: {0}", Platform.Current);
Information("");

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

Task("GitVersion")
    .WithCriteria(!BuildSystem.IsLocalBuild)
    .WithCriteria(!AzurePipelines.IsRunningOnAzurePipelines)
    .Does(() => {
        GitVersion(new GitVersionSettings{
            OutputType = GitVersionOutput.BuildServer
        });
    });

/// <summary>
///  Pre-build setup tasks.
/// </summary>
Task("Setup")
    .IsDependentOn("ValidateMono")
    .IsDependentOn("InstallDotNetCoreSdk");

void InstallDotNetSdk(BuildEnvironment env, BuildPlan plan, string version, string installFolder, bool sharedRuntime = false, bool noPath = false)
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

    if (sharedRuntime)
    {
        argList.Add("-SharedRuntime");
    }

    if (noPath)
    {
        argList.Add("-NoPath");
    }

    Run(env.ShellCommand, $"{env.ShellArgument} {scriptFilePath} {string.Join(" ", argList)}").ExceptionOnError($"Failed to Install .NET Core SDK {version}");
}

Task("InstallDotNetCoreSdk")
    .Does(() =>
{
    if (!useGlobalDotNetSdk)
    {
        foreach (var dotnetVersion in buildPlan.DotNetVersions)
        {
            InstallDotNetSdk(env, buildPlan,
                version: dotnetVersion,
                installFolder: env.Folders.DotNetSdk);
        }

        // Add non-legacy .NET SDK to PATH
        var oldPath = Environment.GetEnvironmentVariable("PATH");
        var newPath = env.Folders.DotNetSdk + (string.IsNullOrEmpty(oldPath) ? "" : System.IO.Path.PathSeparator + oldPath);
        Environment.SetEnvironmentVariable("PATH", newPath);
        Information("PATH: {0}", Environment.GetEnvironmentVariable("PATH"));
    }

    // Disable the first time run experience. We don't need to expand all of .NET Core just to build OmniSharp.
    Environment.SetEnvironmentVariable("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "true");

    Run(env.DotNetCommand, "--info");
});

Task("ValidateMono")
    .WithCriteria(() => !Platform.Current.IsWindows)
    .Does(() =>
{
    ValidateMonoVersion(buildPlan);
});

/// <summary>
///  Prepare test assets.
/// </summary>
Task("PrepareTestAssets")
    .IsDependentOn("Setup");

Task("PrepareTestAssets:CommonTestAssets")
    .IsDependeeOf("PrepareTestAssets")
    .DoesForEach(buildPlan.TestAssets, (project) =>
    {
        Information("Restoring and building: {0}...", project);

        var folder = CombinePaths(env.Folders.TestAssets, "test-projects", project);

        try {
            DotNetCoreBuild(folder, new DotNetCoreBuildSettings()
            {
                ToolPath = env.DotNetCommand,
                WorkingDirectory = folder,
                Verbosity = DotNetCoreVerbosity.Minimal
            });
        } catch {
            // ExternalAlias has issues once in a while, try building again to get it working.
            if (project == "ExternAlias") {

                DotNetCoreBuild(folder, new DotNetCoreBuildSettings()
                {
                    ToolPath = env.DotNetCommand,
                    WorkingDirectory = folder,
                    Verbosity = DotNetCoreVerbosity.Minimal
                });
            }
        }
    });

Task("PrepareTestAssets:RestoreOnlyTestAssets")
    .IsDependeeOf("PrepareTestAssets")
    .DoesForEach(buildPlan.RestoreOnlyTestAssets, (project) =>
    {
        Information("Restoring: {0}...", project);

        var folder = CombinePaths(env.Folders.TestAssets, "test-projects", project);

        DotNetCoreRestore(new DotNetCoreRestoreSettings()
        {
            ToolPath = env.DotNetCommand,
            WorkingDirectory = folder,
            Verbosity = DotNetCoreVerbosity.Minimal
        });
    });

Task("PrepareTestAssets:WindowsOnlyTestAssets")
    .WithCriteria(Platform.Current.IsWindows)
    .IsDependeeOf("PrepareTestAssets")
    .DoesForEach(buildPlan.WindowsOnlyTestAssets, (project) =>
    {
        Information("Restoring and building: {0}...", project);

        var folder = CombinePaths(env.Folders.TestAssets, "test-projects", project);

        DotNetCoreBuild(folder, new DotNetCoreBuildSettings()
        {
            ToolPath = env.DotNetCommand,
            WorkingDirectory = folder,
            Verbosity = DotNetCoreVerbosity.Minimal
        });
    });

Task("PrepareTestAssets:CakeTestAssets")
    .IsDependeeOf("PrepareTestAssets")
    .DoesForEach(buildPlan.CakeTestAssets, (project) =>
    {
        Information("Restoring: {0}...", project);

        var toolsFolder = CombinePaths(env.Folders.TestAssets, "test-projects", project, "tools");
        var packagesConfig = CombinePaths(toolsFolder, "packages.config");

        NuGetInstallFromConfig(packagesConfig, new NuGetInstallSettings {
            OutputDirectory = toolsFolder,
            Prerelease = true,
            Verbosity = NuGetVerbosity.Quiet,
            Source = new[] {
                "https://api.nuget.org/v3/index.json"
            }
        });
    });

void BuildWithDotNetCli(BuildEnvironment env, string configuration)
{
    Information("Building OmniSharp.sln with .NET Core CLI...");

    var logFileNameBase = CombinePaths(env.Folders.ArtifactsLogs, "OmniSharp-build");
    var projectImports = Context.Log.Verbosity == Verbosity.Verbose || Context.Log.Verbosity == Verbosity.Diagnostic
        ? MSBuildBinaryLogImports.Embed
        : MSBuildBinaryLogImports.None;

    var settings = new DotNetCoreMSBuildSettings
    {
        WorkingDirectory = env.WorkingDirectory,

        ArgumentCustomization = args =>
            args.Append("/restore")
                .Append($"/bl:{logFileNameBase}.binlog;ProjectImports={projectImports}")
                .Append($"/v:{Verbosity.Minimal.GetMSBuildVerbosityName()}")
    };

    settings.AddFileLogger(
        new MSBuildFileLoggerSettings {
            AppendToLogFile = false,
            LogFile = logFileNameBase + ".log",
            ShowTimestamp = true,

            // Bug in cake with this
            // Verbosity = Verbosity.Diagnostic,
        }
    );

    settings
        .SetConfiguration(configuration)
        .WithProperty("PackageVersion", env.VersionInfo.NuGetVersion)
        .WithProperty("AssemblyVersion", env.VersionInfo.AssemblySemVer)
        .WithProperty("FileVersion", env.VersionInfo.AssemblySemVer)
        .WithProperty("InformationalVersion", env.VersionInfo.InformationalVersion);

    DotNetCoreMSBuild("OmniSharp.sln", settings);
}

Task("Build")
    .IsDependentOn("Setup")
    .Does(() =>
{
    try
    {
        BuildWithDotNetCli(env, configuration);
    }
    catch
    {
        Error($"Build failed.");
        throw;
    }
});

/// <summary>
///  Run all tests.
/// </summary>
Task("Test")
    .IsDependentOn("Setup")
    .IsDependentOn("Build")
    .IsDependentOn("PrepareTestAssets")
    .Does(() =>
{
        var testTargetFramework = useDotNetTest ? "net6.0" : "net472";
        var testProjects = string.IsNullOrEmpty(testProjectArgument) ? buildPlan.TestProjects : testProjectArgument.Split(',');
        foreach (var testProject in testProjects)
        {
            PrintBlankLine();
            var instanceFolder = CombinePaths(env.Folders.Bin, configuration, testProject, testTargetFramework);
            var targetPath = CombinePaths(instanceFolder, $"{testProject}.dll");

            if (useDotNetTest)
            {
                var logFile = CombinePaths(env.Folders.ArtifactsLogs, $"{testProject}-netsdk-result.xml");
                var arguments = $"test \"{targetPath}\" --logger \"console;verbosity=normal\" --logger \"trx;LogFileName={logFile}\" --blame-hang-timeout 60sec";

                Console.WriteLine($"Executing: dotnet {arguments}");

                Run("dotnet", arguments, instanceFolder)
                    .ExceptionOnError($"Test {testProject} failed for {testTargetFramework}");
            }
            else
            {
                var logFile = CombinePaths(env.Folders.ArtifactsLogs, $"{testProject}-desktop-result.xml");

                // Copy xunit executable to test folder to solve path errors
                var xunitToolsFolder = CombinePaths(env.Folders.Tools, "xunit.runner.console", "tools", "net452");
                var xunitInstancePath = CombinePaths(instanceFolder, "xunit.console.exe");
                FileHelper.Copy(CombinePaths(xunitToolsFolder, "xunit.console.exe"), xunitInstancePath, overwrite: true);
                FileHelper.Copy(CombinePaths(xunitToolsFolder, "xunit.runner.utility.net452.dll"), CombinePaths(instanceFolder, "xunit.runner.utility.net452.dll"), overwrite: true);
                var arguments = $"\"{targetPath}\" -noshadow -parallel none -xml \"{logFile}\" -notrait category=failing";

                if (Platform.Current.IsWindows)
                {
                    Run(xunitInstancePath, arguments, instanceFolder)
                        .ExceptionOnError($"Test {testProject} failed for {testTargetFramework}");
                }
                else
                {
                    Run("mono", $"\"{xunitInstancePath}\" {arguments}", instanceFolder)
                        .ExceptionOnError($"Test {testProject} failed for net472");
                }
            }
        }
});

void CopyMonoBuild(BuildEnvironment env, string sourceFolder, string outputFolder, string platformName = null)
{
    DirectoryHelper.Copy(sourceFolder, outputFolder, copySubDirectories: false);
}

void CopyExtraDependencies(BuildEnvironment env, string outputFolder)
{
    // copy license
    FileHelper.Copy(CombinePaths(env.WorkingDirectory, "license.md"), CombinePaths(outputFolder, "license.md"), overwrite: true);
}

void AddOmniSharpBindingRedirects(string omnisharpFolder)
{
    var appConfig = CombinePaths(omnisharpFolder, "OmniSharp.exe.config");
    if (!FileHelper.Exists(appConfig))
    {
        appConfig = CombinePaths(omnisharpFolder, "OmniSharp.dll.config");
    }

    // Load app.config
    var document = new XmlDocument();
    document.Load(appConfig);

    // Find bindings
    var runtime = document.GetElementsByTagName("runtime")[0];
    var assemblyBinding = document.CreateElement("assemblyBinding", "urn:schemas-microsoft-com:asm.v1");

    // Find OmniSharp libraries
    foreach (var filePath in System.IO.Directory.GetFiles(omnisharpFolder, "OmniSharp.*.dll"))
    {
        // Read assembly name from OmniSharp library
        var assemblyName = AssemblyName.GetAssemblyName(filePath);

        // Create binding redirect and add to bindings
        var redirect = CreateBindingRedirect(document, assemblyName);
        assemblyBinding.AppendChild(redirect);
    }

    runtime.AppendChild(assemblyBinding);

    // Save updated app.config
    document.Save(appConfig);
}

XmlElement CreateBindingRedirect(XmlDocument document, AssemblyName assemblyName)
{
    var dependentAssembly = document.CreateElement("dependentAssembly", "urn:schemas-microsoft-com:asm.v1");

    var assemblyIdentity = document.CreateElement("assemblyIdentity", "urn:schemas-microsoft-com:asm.v1");
    assemblyIdentity.SetAttribute("name", assemblyName.Name);
    var publicKeyToken = BitConverter.ToString(assemblyName.GetPublicKeyToken()).Replace("-", string.Empty).ToLower();
    assemblyIdentity.SetAttribute("publicKeyToken", publicKeyToken);
    assemblyIdentity.SetAttribute("culture", "neutral");
    dependentAssembly.AppendChild(assemblyIdentity);

    var bindingRedirect = document.CreateElement("bindingRedirect", "urn:schemas-microsoft-com:asm.v1");
    bindingRedirect.SetAttribute("oldVersion", $"0.0.0.0-{assemblyName.Version}");
    bindingRedirect.SetAttribute("newVersion", assemblyName.Version.ToString());
    dependentAssembly.AppendChild(bindingRedirect);

    return dependentAssembly;
}

string PublishMonoBuild(string project, BuildEnvironment env, BuildPlan plan, string configuration)
{
    Information($"Publishing Mono build for {project}...");

    var outputFolder = CombinePaths(env.Folders.ArtifactsPublish, project, "mono");

    var buildFolder = CombinePaths(env.Folders.Bin, configuration, project, "net472");

    DirectoryHelper.Copy(buildFolder, outputFolder, copySubDirectories: false);

    CopyExtraDependencies(env, outputFolder);
    AddOmniSharpBindingRedirects(outputFolder);

    Package(project, "mono", outputFolder, env.Folders.ArtifactsPackage, env.Folders.DeploymentPackage);

    return outputFolder;
}

string PublishMonoBuildForPlatform(string project, MonoRuntime monoRuntime, BuildEnvironment env, BuildPlan plan)
{
    Information("Publishing platform-specific Mono build: {0}", monoRuntime.PlatformName);

    var outputFolder = CombinePaths(env.Folders.ArtifactsPublish, project, monoRuntime.PlatformName);

    CopyExtraDependencies(env, outputFolder);
    AddOmniSharpBindingRedirects(outputFolder);

    Package(project, monoRuntime.PlatformName, outputFolder, env.Folders.ArtifactsPackage, env.Folders.DeploymentPackage);

    return outputFolder;
}

Task("PublishMonoBuilds")
    .IsDependentOn("Setup")
    .WithCriteria(() => !Platform.Current.IsWindows)
    .Does(() =>
{
    foreach (var project in buildPlan.HostProjects)
    {
        var outputFolder = PublishMonoBuild(project, env, buildPlan, configuration);

        CreateRunScript(project, outputFolder, env.Folders.ArtifactsScripts);

        if (publishAll)
        {
            foreach (var monoRuntime in env.BuildMonoRuntimes)
            {
                PublishMonoBuildForPlatform(project, monoRuntime, env, buildPlan);
            }
        }
    }
});

Task("PublishNet6Builds")
    .IsDependentOn("Setup")
    .Does(() =>
{
    foreach (var project in buildPlan.HostProjects)
    {
        if (publishAll)
        {
            if (!Platform.Current.IsWindows)
            {
                PublishBuild(project, env, buildPlan, configuration, "linux-x64", "net6.0");
                PublishBuild(project, env, buildPlan, configuration, "linux-arm64", "net6.0");
                PublishBuild(project, env, buildPlan, configuration, "osx-x64", "net6.0");
                PublishBuild(project, env, buildPlan, configuration, "osx-arm64", "net6.0");
            }
            else
            {
                PublishBuild(project, env, buildPlan, configuration, "win7-x86", "net6.0");
                PublishBuild(project, env, buildPlan, configuration, "win7-x64", "net6.0");
                PublishBuild(project, env, buildPlan, configuration, "win10-arm64", "net6.0");
            }
        }
        else if (Platform.Current.IsWindows)
        {
            if (Platform.Current.IsX86)
            {
                PublishBuild(project, env, buildPlan, configuration, "win7-x86", "net6.0");
            }
            else if (Platform.Current.IsX64)
            {
                PublishBuild(project, env, buildPlan, configuration, "win7-x64", "net6.0");
            }
            else
            {
                PublishBuild(project, env, buildPlan, configuration, "win10-arm64", "net6.0");
            }
        }
        else
        {
            if (Platform.Current.IsMacOS)
            {
                PublishBuild(project, env, buildPlan, configuration, "osx-x64", "net6.0");
                PublishBuild(project, env, buildPlan, configuration, "osx-arm64", "net6.0");
            }
            else
            {
                PublishBuild(project, env, buildPlan, configuration, "linux-x64", "net6.0");
                PublishBuild(project, env, buildPlan, configuration, "linux-arm64", "net6.0");
            }
        }

    }
});

string PublishBuild(string project, BuildEnvironment env, BuildPlan plan, string configuration, string rid, string framework)
{
    var projectName = project + ".csproj";
    var projectFileName = CombinePaths(env.Folders.Source, project, projectName);
    var outputFolder = CombinePaths(env.Folders.ArtifactsPublish, project, rid, framework);

    Information("Publishing {0} for {1}...", projectName, rid);

    try
    {
        var publishSettings = new DotNetCorePublishSettings()
        {
            Framework = framework,
            Runtime = rid, // TODO: With everything today do we need to publish this with a rid?  This appears to be legacy bit when we used to push for all supported dotnet core rids.
            PublishReadyToRun = true, // Improve startup performance by applying some AOT compilation
            SelfContained = false, // Since we are specifying a runtime identifier this defaults to true. We don't need to ship a runtime for net6 because we require the .NET SDK to be installed.
            Configuration = configuration,
            OutputDirectory = outputFolder,
            MSBuildSettings = new DotNetCoreMSBuildSettings()
                .WithProperty("PackageVersion", env.VersionInfo.NuGetVersion)
                .WithProperty("AssemblyVersion", env.VersionInfo.AssemblySemVer)
                .WithProperty("FileVersion", env.VersionInfo.AssemblySemVer)
                .WithProperty("InformationalVersion", env.VersionInfo.InformationalVersion),
            ToolPath = env.DotNetCommand,
            WorkingDirectory = env.WorkingDirectory,
            Verbosity = DotNetCoreVerbosity.Minimal,
        };
        DotNetCorePublish(projectFileName, publishSettings);
    }
    catch
    {
        Error($"Failed to publish {project} for {rid}");
        throw;
    }

    if (framework is "net6.0")
    {
        // Delete NuGet libraries so they can be loaded from SDK folder.
        foreach (var filePath in DirectoryHelper.GetFiles(outputFolder, "NuGet.*.dll"))
        {
            FileHelper.Delete(filePath);
        }

        foreach (var filePath in DirectoryHelper.GetFiles(outputFolder, "System.Configuration.ConfigurationManager.dll"))
        {
            FileHelper.Delete(filePath);
        }
    }

    CopyExtraDependencies(env, outputFolder);
    AddOmniSharpBindingRedirects(outputFolder);

    var platformFolder = framework != "net472" ? $"{rid}-{framework}" : rid;
    Package(project, platformFolder, outputFolder, env.Folders.ArtifactsPackage, env.Folders.DeploymentPackage);

    return outputFolder;
}

Task("PublishWindowsBuilds")
    .WithCriteria(() => Platform.Current.IsWindows)
    .IsDependentOn("Setup")
    .Does(() =>
{
    foreach (var project in buildPlan.HostProjects)
    {
        string outputFolder;

        if (publishAll)
        {
            var outputFolderX86 = PublishBuild(project, env, buildPlan, configuration, "win7-x86", "net472");
            var outputFolderX64 = PublishBuild(project, env, buildPlan, configuration, "win7-x64", "net472");
            var outputFolderArm64 = PublishBuild(project, env, buildPlan, configuration, "win10-arm64", "net472");

            outputFolder = Platform.Current.IsX86
                ? outputFolderX86
                : Platform.Current.IsX64
                    ? outputFolderX64
                    : outputFolderArm64;
        }
        else if (Platform.Current.IsX86)
        {
            outputFolder = PublishBuild(project, env, buildPlan, configuration, "win7-x86", "net472");
        }
        else if (Platform.Current.IsX64)
        {
            outputFolder = PublishBuild(project, env, buildPlan, configuration, "win7-x64", "net472");
        }
        else
        {
            outputFolder = PublishBuild(project, env, buildPlan, configuration, "win10-arm64", "net472");
        }

        CreateRunScript(project, outputFolder, env.Folders.ArtifactsScripts);
    }
});

Task("PublishNuGet")
    .IsDependentOn("InstallDotNetCoreSdk")
    .Does(() => {
        DotNetCorePack(".", new DotNetCorePackSettings() {
            Configuration = "Release",
            OutputDirectory = "./artifacts/nuget/",
            MSBuildSettings = new DotNetCoreMSBuildSettings()
                .SetConfiguration(configuration)
                .WithProperty("PackageVersion", env.VersionInfo.NuGetVersion)
                .WithProperty("AssemblyVersion", env.VersionInfo.AssemblySemVer)
                .WithProperty("FileVersion", env.VersionInfo.AssemblySemVer)
                .WithProperty("InformationalVersion", env.VersionInfo.InformationalVersion),
        });
    });

Task("Publish")
    .IsDependentOn("Build")
    .IsDependentOn("PublishMonoBuilds")
    .IsDependentOn("PublishNet6Builds")
    .IsDependentOn("PublishWindowsBuilds")
    .IsDependentOn("PublishNuGet");

/// <summary>
///  Execute the run script.
/// </summary>
Task("ExecuteRunScript")
    .Does(() =>
{
    // TODO: Pass configuration into run script to ensure that MSBuild output paths are handled correctly.
    // Otherwise, we get "could not resolve output path" messages when building for release.

    foreach (var project in buildPlan.HostProjects)
    {
        var projectFolder = CombinePaths(env.Folders.Source, project);

        var scriptPath = GetScriptPath(env.Folders.ArtifactsScripts, project);
        var didNotExitWithError = Run(env.ShellCommand, $"{env.ShellArgument} \"{scriptPath}\" -s \"{projectFolder}\"",
                                    new RunOptions(waitForIdle: true))
                                .WasIdle;
        if (!didNotExitWithError)
        {
            throw new Exception($"Failed to run {scriptPath}");
        }
    }
});

/// <summary>
///  Quick build.
/// </summary>
Task("Quick")
    .IsDependentOn("Cleanup")
    .IsDependentOn("Publish");

/// <summary>
///  Clean install path.
/// </summary>
Task("CleanupInstall")
    .Does(() =>
{
    DirectoryHelper.ForceCreate(installFolder);
});

/// <summary>
///  Quick build + install.
/// </summary>
Task("Install")
    .IsDependentOn("Cleanup")
    .IsDependentOn("Publish")
    .IsDependentOn("CleanupInstall")
    .Does(() =>
{
    foreach (var project in buildPlan.HostProjects)
    {
        string platform;
        if (Platform.Current.IsWindows)
        {
            platform = Platform.Current.IsX86
                ? "win7-x86"
                : Platform.Current.IsX64
                    ? "win7-x64"
                    : "win10-arm64";
        }
        else
        {
            platform = "mono";
        }

        var outputFolder = PathHelper.GetFullPath(CombinePaths(env.Folders.ArtifactsPublish, project, platform));
        var targetFolder = PathHelper.GetFullPath(CombinePaths(installFolder));

        DirectoryHelper.Copy(outputFolder, targetFolder);

        CreateRunScript(project, installFolder, env.Folders.ArtifactsScripts);

        Information($"OmniSharp is installed locally at {installFolder}");
    }
});

/// <summary>
///  Full build and execute script at the end.
/// </summary>
Task("All")
    .IsDependentOn("Cleanup")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Publish")
    .IsDependentOn("ExecuteRunScript");

/// <summary>
///  Default Task aliases to All.
/// </summary>
Task("Default")
    .IsDependentOn("All");

/// <summary>
///  Task aliases for CI (excluding tests) as they are parallelized
/// </summary>
Task("CI")
    .IsDependentOn("Cleanup")
    .IsDependentOn("Build")
    .IsDependentOn("Publish")
    .IsDependentOn("ExecuteRunScript");

Teardown(context =>
{
    // Ensure that we shutdown all build servers used by the CLI during build.
    Run(env.DotNetCommand, "build-server shutdown");
});

/// <summary>
///  Default to All.
/// </summary>
RunTarget(target);
