#addin "Newtonsoft.Json"

#load "platform.cake"

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;

public static class Log
{
    public static ICakeContext Context { get; set; }

    public static void Write(Verbosity verbosity, LogLevel logLevel, string message, params object[] args) =>
        Context.Log.Write(verbosity, logLevel, message, args);

    public static void Debug(Verbosity verbosity, string message, params object[] args) =>
        Write(verbosity, LogLevel.Debug, message, args);
}

public static class FileHelper
{
    public static void Copy(string source, string destination, bool overwrite = false)
    {
        Log.Debug(Verbosity.Diagnostic, "Copy file: {0} to {1}.", source, destination);
        System.IO.File.Copy(source, destination, overwrite);
    }

    public static void Delete(string path)
    {
        Log.Debug(Verbosity.Diagnostic, "Delete file: {0}.", path);
        System.IO.File.Delete(path);
    }

    public static bool Exists(string path) =>
        System.IO.File.Exists(path);

    public static void WriteAllLines(string path, string[] contents) =>
        System.IO.File.WriteAllLines(path, contents);
}

public static class DirectoryHelper
{
    public static void Copy(string source, string destination, bool copySubDirectories = true)
    {
        var files = System.IO.Directory.GetFiles(source);
        var subDirectories = System.IO.Directory.GetDirectories(source);

        if (!Exists(destination))
        {
            Create(destination);
        }

        foreach (var file in files)
        {
            var newFile = PathHelper.Combine(destination, PathHelper.GetFileName(file));
            FileHelper.Copy(file, newFile, overwrite: true);
        }

        if (copySubDirectories)
        {
            foreach (var subDirectory in subDirectories)
            {
                var newSubDirectory = PathHelper.Combine(destination, PathHelper.GetFileName(subDirectory));
                Copy(subDirectory, newSubDirectory);
            }
        }
    }

    public static void Create(string path)
    {
        Log.Debug(Verbosity.Diagnostic, "Create directory: {0}.", path);
        System.IO.Directory.CreateDirectory(path);
    }

    public static void Delete(string path, bool recursive)
    {
        Log.Debug(Verbosity.Diagnostic, "Delete directory: {0}.", path);
        System.IO.Directory.Delete(path, recursive);
    }

    public static bool Exists(string path) =>
        System.IO.Directory.Exists(path);

    public static void ForceCreate(string path)
    {
        if (Exists(path))
        {
            Delete(path, recursive: true);
        }

        Create(path);
    }
}

public static class PathHelper
{
    public static string Combine(params string[] paths) =>
        System.IO.Path.Combine(paths);

    public static string GetDirectoryName(string path) =>
        System.IO.Path.GetDirectoryName(path);

    public static string GetFileName(string path) =>
        System.IO.Path.GetFileName(path);

    public static string GetFullPath(string path) =>
        System.IO.Path.GetFullPath(path);
}

string CombinePaths(params string[] paths)
{
    return PathHelper.Combine(paths);
}

void DownloadFileAndUnzip(string url, string folder)
{
    DirectoryHelper.ForceCreate(folder);
    var zipFileName = CombinePaths(folder, "temp.zip");

    Information("Downloading {0}", url);

    using (var client = new WebClient())
    {
        client.DownloadFile(url, zipFileName);
    }

    Unzip(zipFileName, folder);
    FileHelper.Delete(zipFileName);
}

public class Folders
{
    public string DotNetSdk { get; }
    public string LegacyDotNetSdk { get; }
    public string Mono { get; }
    public string MSBuild { get; }
    public string Tools { get; }

    public string Source { get; }
    public string Tests { get; }
    public string TestAssets { get; }

    public string Artifacts { get; }
    public string ArtifactsPublish { get; }
    public string ArtifactsLogs { get; }
    public string ArtifactsPackage { get; }
    public string ArtifactsScripts { get; }

    public string MonoRuntimeMacOS { get; }
    public string MonoRuntimeLinux32 { get; }
    public string MonoRuntimeLinux64 { get; }
    public string MonoFramework { get; }
    public string MonoMSBuildRuntime { get; }
    public string MonoMSBuildLib { get; }

    public Folders(string workingDirectory)
    {
        this.DotNetSdk = PathHelper.Combine(workingDirectory, ".dotnet");
        this.LegacyDotNetSdk = PathHelper.Combine(workingDirectory, ".dotnet-legacy");
        this.Mono = PathHelper.Combine(workingDirectory, ".mono");
        this.MSBuild = PathHelper.Combine(workingDirectory, ".msbuild");
        this.Tools = PathHelper.Combine(workingDirectory, "tools");

        this.Source = PathHelper.Combine(workingDirectory, "src");
        this.Tests = PathHelper.Combine(workingDirectory, "tests");
        this.TestAssets = PathHelper.Combine(workingDirectory, "test-assets");

        this.Artifacts = PathHelper.Combine(workingDirectory, "artifacts");
        this.ArtifactsPublish = PathHelper.Combine(this.Artifacts, "publish");
        this.ArtifactsLogs = PathHelper.Combine(this.Artifacts, "logs");
        this.ArtifactsPackage = PathHelper.Combine(this.Artifacts, "package");
        this.ArtifactsScripts = PathHelper.Combine(this.Artifacts, "scripts");

        this.MonoRuntimeMacOS = PathHelper.Combine(this.Tools, "Mono.Runtime.MacOS");
        this.MonoRuntimeLinux32 = PathHelper.Combine(this.Tools, "Mono.Runtime.Linux-x86");
        this.MonoRuntimeLinux64 = PathHelper.Combine(this.Tools, "Mono.Runtime.Linux-x64");
        this.MonoFramework = PathHelper.Combine(this.Tools, "Mono.Framework");
        this.MonoMSBuildRuntime = PathHelper.Combine(this.Tools, "Microsoft.Build.Runtime.Mono");
        this.MonoMSBuildLib = PathHelper.Combine(this.Tools, "Microsoft.Build.Lib.Mono");
    }
}

public class MonoRuntime
{
    public string PlatformName { get; }
    public string InstallFolder { get; }
    public string RuntimeFile { get; }

    public MonoRuntime(string platformName, string installFolder, string runtimeFile)
    {
        this.PlatformName = platformName;
        this.InstallFolder = installFolder;
        this.RuntimeFile = runtimeFile;
    }
}

public class BuildEnvironment
{
    public string WorkingDirectory { get; }
    public Folders Folders { get; }

    public string DotNetCommand { get; }
    public string LegacyDotNetCommand { get; }

    public string ShellCommand { get; }
    public string ShellArgument { get; }
    public string ShellScriptFileExtension { get; }

    public MonoRuntime[] MonoRuntimes { get; }
    public MonoRuntime CurrentMonoRuntime { get; }

    public BuildEnvironment(bool useGlobalDotNetSdk)
    {
        this.WorkingDirectory = PathHelper.GetFullPath(
            System.IO.Directory.GetCurrentDirectory());
        this.Folders = new Folders(this.WorkingDirectory);

        this.DotNetCommand = useGlobalDotNetSdk
            ? "dotnet"
            : PathHelper.Combine(this.Folders.DotNetSdk, "dotnet");

        this.LegacyDotNetCommand = PathHelper.Combine(this.Folders.LegacyDotNetSdk, "dotnet");

        this.ShellCommand = Platform.Current.IsWindows ? "powershell" : "bash";
        this.ShellArgument = Platform.Current.IsWindows ? "-NoProfile /Command" : "-C";
        this.ShellScriptFileExtension = Platform.Current.IsWindows ? "ps1" : "sh";

        this.MonoRuntimes = new []
        {
            new MonoRuntime("osx", this.Folders.MonoRuntimeMacOS, "mono.osx"),
            new MonoRuntime("linux-x86", this.Folders.MonoRuntimeLinux32, "mono.linux-x86"),
            new MonoRuntime("linux-x64", this.Folders.MonoRuntimeLinux64, "mono.linux-x86_64")
        };

        if (Platform.Current.IsMacOS)
        {
            this.CurrentMonoRuntime = this.MonoRuntimes[0];
        }
        else if (Platform.Current.IsLinux && Platform.Current.Is32Bit)
        {
            this.CurrentMonoRuntime = this.MonoRuntimes[1];
        }
        else if (Platform.Current.IsLinux && Platform.Current.Is64Bit)
        {
            this.CurrentMonoRuntime = this.MonoRuntimes[2];
        }
    }
}

/// <summary>
///  Class representing build.json
/// </summary>
public class BuildPlan
{
    public string DotNetInstallScriptURL { get; set; }
    public string DotNetChannel { get; set; }
    public string DotNetVersion { get; set; }
    public string LegacyDotNetVersion { get; set; }
    public string RequiredMonoVersion { get; set; }
    public string DownloadURL { get; set; }
    public string MonoRuntimeMacOS { get; set; }
    public string MonoRuntimeLinux32 { get; set; }
    public string MonoRuntimeLinux64 { get; set; }
    public string MonoFramework { get; set; }
    public string MonoMSBuildRuntime { get; set; }
    public string MonoMSBuildLib { get; set; }
    public string MainProject { get; set; }
    public string[] TestProjects { get; set; }
    public string[] TestAssets { get; set; }
    public string[] LegacyTestAssets { get; set; }

    public static BuildPlan Load(BuildEnvironment env)
    {
        var buildJsonPath = PathHelper.Combine(env.WorkingDirectory, "build.json");
        return JsonConvert.DeserializeObject<BuildPlan>(
            System.IO.File.ReadAllText(buildJsonPath));
    }
}

void PrintBlankLine()
{
    Information("");
}