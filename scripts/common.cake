#addin "nuget:?package=Newtonsoft.Json&version=13.0.4"
#load "platform.cake"

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    public static string[] GetFiles(string path, string searchPattern = "*.*")
    {
        return System.IO.Directory.GetFiles(path, searchPattern);
    }

    public static void Copy(string source, string destination, bool copySubDirectories = true, bool replaceFiles = true)
    {
        var files = GetFiles(source);
        var subDirectories = System.IO.Directory.GetDirectories(source);

        if (!Exists(destination))
        {
            Create(destination);
        }

        foreach (var file in files)
        {
            var newFile = PathHelper.Combine(destination, PathHelper.GetFileName(file));
            if (replaceFiles || !FileHelper.Exists(newFile))
            {
                FileHelper.Copy(file, newFile, overwrite: true);
            }
        }

        if (copySubDirectories)
        {
            foreach (var subDirectory in subDirectories)
            {
                var newSubDirectory = PathHelper.Combine(destination, PathHelper.GetFileName(subDirectory));
                Copy(subDirectory, newSubDirectory, copySubDirectories, replaceFiles);
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

public class Folders
{
    public string DotNetSdk { get; }
    public string Tools { get; }

    public string Bin { get; }
    public string Source { get; }
    public string Tests { get; }
    public string TestAssets { get; }

    public string Artifacts { get; }
    public string ArtifactsPublish { get; }
    public string ArtifactsLogs { get; }
    public string ArtifactsPackage { get; }
    public string DeploymentPackage { get; }
    public string ArtifactsScripts { get; }

    public Folders(string workingDirectory)
    {
        this.DotNetSdk = PathHelper.Combine(workingDirectory, ".dotnet");
        this.Tools = PathHelper.Combine(workingDirectory, "tools");

        this.Bin = PathHelper.Combine(workingDirectory, "bin");
        this.Source = PathHelper.Combine(workingDirectory, "src");
        this.Tests = PathHelper.Combine(workingDirectory, "tests");
        this.TestAssets = PathHelper.Combine(workingDirectory, "test-assets");

        this.Artifacts = PathHelper.Combine(workingDirectory, "artifacts");
        this.ArtifactsPublish = PathHelper.Combine(this.Artifacts, "publish");
        this.ArtifactsLogs = PathHelper.Combine(this.Artifacts, "logs");
        this.ArtifactsPackage = PathHelper.Combine(this.Artifacts, "package");
        this.DeploymentPackage = PathHelper.Combine(this.Artifacts, "deployment");
        this.ArtifactsScripts = PathHelper.Combine(this.Artifacts, "scripts");
    }
}

public class BuildEnvironment
{
    public string WorkingDirectory { get; }
    public Folders Folders { get; }

    public string TargetFramework { get; }

    public string DotNetCommand { get; }

    public string ShellCommand { get; }
    public string ShellArgument { get; }
    public string ShellScriptFileExtension { get; }

    private readonly Lazy<GitVersion> _versionInfo;
    public GitVersion VersionInfo => _versionInfo.Value;

    public BuildEnvironment(bool useGlobalDotNetSdk, ICakeContext context)
    {
        this.WorkingDirectory = context.Environment.WorkingDirectory.FullPath;
        this.Folders = new Folders(this.WorkingDirectory);

        this.TargetFramework = GetTargetFramework(this.WorkingDirectory);

        this.DotNetCommand = useGlobalDotNetSdk
            ? "dotnet"
            : PathHelper.Combine(this.Folders.DotNetSdk, "dotnet");
        if (Platform.Current.IsWindows) this.DotNetCommand += ".exe";

        this.ShellCommand = Platform.Current.IsWindows ? "powershell" : "bash";
        this.ShellArgument = Platform.Current.IsWindows ? "-NoProfile -ExecutionPolicy Bypass /Command" : "-C";
        this.ShellScriptFileExtension = Platform.Current.IsWindows ? "ps1" : "sh";

        _versionInfo = new Lazy<GitVersion>(() => GetGitVersionFromEnvironment(context));
    }

    private static string GetTargetFramework(string workingDirectory)
    {
        var settingsFile = PathHelper.Combine(workingDirectory, "build", "Settings.props");
        var document = new System.Xml.XmlDocument();
        document.Load(settingsFile);

        var namespaces = new System.Xml.XmlNamespaceManager(document.NameTable);
        namespaces.AddNamespace("msbuild", "http://schemas.microsoft.com/developer/msbuild/2003");
        var targetFramework = document.SelectSingleNode("/msbuild:Project/msbuild:PropertyGroup/msbuild:TargetFramework", namespaces);

        return targetFramework?.InnerText
            ?? throw new Exception($"TargetFramework was not found in '{settingsFile}'.");
    }

    private static bool HasGitVer(ICakeContext context)
    {
        var envVars = context.EnvironmentVariables();
        return envVars.Keys.Join(GitVersionKeys, z => z, z => z, (a, b) => a, StringComparer.OrdinalIgnoreCase).Any();
    }

    private static GitVersion GetGitVersionFromEnvironment(ICakeContext context)
    {
        if (HasGitVer(context))
        {
            var environmentVariables = context.Environment.GetEnvironmentVariables();
            return new GitVersion()
            {
                Major = int.Parse(GetGitVersionValue(environmentVariables, "Major")),
                Minor = int.Parse(GetGitVersionValue(environmentVariables, "Minor")),
                Patch = int.Parse(GetGitVersionValue(environmentVariables, "Patch")),
                PreReleaseTag = GetGitVersionValue(environmentVariables, "PreReleaseTag"),
                PreReleaseTagWithDash = GetGitVersionValue(environmentVariables, "PreReleaseTagWithDash"),
                PreReleaseLabel = GetGitVersionValue(environmentVariables, "PreReleaseLabel"),
                PreReleaseNumber = GetGitVersionNullableInt(environmentVariables, "PreReleaseNumber"),
                BuildMetaData = GetGitVersionValue(environmentVariables, "BuildMetaData"),
                FullBuildMetaData = GetGitVersionValue(environmentVariables, "FullBuildMetaData"),
                MajorMinorPatch = GetGitVersionValue(environmentVariables, "MajorMinorPatch"),
                SemVer = GetGitVersionValue(environmentVariables, "SemVer"),
                AssemblySemVer = GetGitVersionValue(environmentVariables, "AssemblySemVer"),
                FullSemVer = GetGitVersionValue(environmentVariables, "FullSemVer"),
                InformationalVersion = GetGitVersionValue(environmentVariables, "InformationalVersion"),
                BranchName = GetGitVersionValue(environmentVariables, "BranchName"),
                Sha = GetGitVersionValue(environmentVariables, "Sha"),
                NuGetVersion = GetGitVersionValue(environmentVariables, "NuGetVersion"),
                CommitsSinceVersionSource = GetGitVersionNullableInt(environmentVariables, "CommitsSinceVersionSource"),
                CommitDate = GetGitVersionValue(environmentVariables, "CommitDate"),
            };
        }
        else
        {
            return GetGitVersionFromTool(context);
        }
    }

    private static GitVersion GetGitVersionFromTool(ICakeContext context)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "tool run dotnet-gitversion --output json",
            WorkingDirectory = context.Environment.WorkingDirectory.FullPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using (var process = new System.Diagnostics.Process { StartInfo = startInfo })
        {
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            var output = outputTask.GetAwaiter().GetResult();
            var error = errorTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                throw new Exception($"GitVersion failed with exit code {process.ExitCode}: {error}");
            }

            var version = JObject.Parse(output);
            var semVer = (string)version["SemVer"];
            if (string.IsNullOrWhiteSpace(semVer))
            {
                throw new Exception("GitVersion metadata does not contain a package version.");
            }

            return new GitVersion
            {
                Major = (int)version["Major"],
                Minor = (int)version["Minor"],
                Patch = (int)version["Patch"],
                PreReleaseTag = (string)(version["PreReleaseTag"] ?? version["PreReleaseLabel"]),
                PreReleaseTagWithDash = (string)(version["PreReleaseTagWithDash"] ?? version["PreReleaseLabelWithDash"]),
                PreReleaseLabel = (string)(version["PreReleaseLabelName"] ?? version["PreReleaseLabel"]),
                PreReleaseNumber = (int?)version["PreReleaseNumber"],
                BuildMetaData = (string)version["BuildMetaData"],
                FullBuildMetaData = (string)version["FullBuildMetaData"],
                MajorMinorPatch = (string)version["MajorMinorPatch"],
                SemVer = semVer,
                AssemblySemVer = (string)version["AssemblySemVer"],
                FullSemVer = (string)version["FullSemVer"],
                InformationalVersion = (string)version["InformationalVersion"],
                BranchName = (string)version["BranchName"],
                Sha = (string)version["Sha"],
                NuGetVersion = (string)(version["NuGetVersion"] ?? version["SemVer"]),
                CommitsSinceVersionSource = (int?)(version["CommitsSinceVersionSource"] ?? version["VersionSourceDistance"]),
                CommitDate = (string)version["CommitDate"]
            };
        }
    }

    private static string GetGitVersionValue(IDictionary<string, string> environmentVariables, string key)
    {
        var value = environmentVariables.FirstOrDefault(x => x.Key.Equals($"GitVersion_{key}", StringComparison.OrdinalIgnoreCase));
        return value.Value;
    }

    private static int? GetGitVersionNullableInt(IDictionary<string, string> environmentVariables, string key)
    {
        var value = GetGitVersionValue(environmentVariables, key);
        return string.IsNullOrWhiteSpace(value) ? null : int.Parse(value) as int?;
    }

    private static readonly string[] GitVersionKeys = {
        "GITVERSION_MAJOR",
        "GITVERSION_MINOR",
        "GITVERSION_PATCH",
        "GITVERSION_PRERELEASETAG",
        "GITVERSION_PRERELEASETAGWITHDASH",
        "GITVERSION_PRERELEASELABEL",
        "GITVERSION_PRERELEASENUMBER",
        "GITVERSION_BUILDMETADATA",
        "GITVERSION_FULLBUILDMETADATA",
        "GITVERSION_MAJORMINORPATCH",
        "GITVERSION_SEMVER",
        "GITVERSION_ASSEMBLYSEMVER",
        "GITVERSION_FULLSEMVER",
        "GITVERSION_INFORMATIONALVERSION",
        "GITVERSION_BRANCHNAME",
        "GITVERSION_SHA",
        "GITVERSION_NUGETVERSION",
        "GITVERSION_COMMITSSINCEVERSIONSOURCE",
        "GITVERSION_COMMITDATE",
    };
}

/// <summary>
///  Class representing build.json
/// </summary>
public class BuildPlan
{
    public string DotNetInstallScriptURL { get; set; }
    public string DotNetChannel { get; set; }
    public string[] DotNetVersions { get; set; }
    public string DownloadURL { get; set; }
    public string[] HostProjects { get; set; }
    public string[] TestProjects { get; set; }
    public string[] TestAssets { get; set; }
    public string[] CakeTestAssets { get; set; }
    public string[] WindowsOnlyTestAssets { get; set; }
    public string[] RestoreOnlyTestAssets { get; set; }

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
