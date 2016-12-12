#load "runhelpers.cake"

using System.Net;

/// <summary>
/// Downloads and unzips a NuGet package directly without any dependencies.
/// </summary>
void DownloadNuGetPackage(string packageID, string version, string outputDirectory, string feedUrl)
{
    var outputFolder = System.IO.Path.Combine(outputDirectory, packageID);
    var outputFileName = System.IO.Path.ChangeExtension(outputFolder, "nupkg");

    if (DirectoryExists(outputFolder))
    {
        DeleteDirectory(outputFolder, recursive: true);
    }

    using (var client = new WebClient())
    {
        client.DownloadFile(
            address: $"{feedUrl}/{packageID}/{version}",
            fileName: outputFileName);
    }

    Unzip(outputFileName, outputFolder);
}

private ExitStatus RunNuGetInstall(string packageIdOConfigFilePath, string version, bool excludeVersion, bool noCache, bool prerelease, string outputDirectory)
{
    var nugetPath = Environment.GetEnvironmentVariable("NUGET_EXE");

    var argList = new List<string> { "install", packageIdOConfigFilePath };

    if (!string.IsNullOrWhiteSpace(version))
    {
        argList.Add("-Version");
        argList.Add(version);
    }

    if (excludeVersion)
    {
        argList.Add("-ExcludeVersion");
    }

    if (noCache)
    {
        argList.Add("-NoCache");
    }

    if (prerelease)
    {
        argList.Add("-Prerelease");
    }

    if (!string.IsNullOrWhiteSpace(outputDirectory))
    {
        argList.Add("-OutputDirectory");
        argList.Add(outputDirectory);
    }

    var arguments = string.Join(" ", argList);

    return IsRunningOnWindows()
        ? Run(nugetPath, arguments)
        : Run("mono", $"\"{nugetPath}\" {arguments}");
}

ExitStatus InstallNuGetPackage(string packageID, string version = null, bool excludeVersion = false, bool noCache = false, bool prerelease = false, string outputDirectory = null)
{
    return RunNuGetInstall(packageID, version, excludeVersion, noCache, prerelease, outputDirectory);
}

ExitStatus InstallNuGetPackages(string configFilePath, bool excludeVersion = false, bool noCache = false, string outputDirectory = null)
{
    return RunNuGetInstall(configFilePath, null, excludeVersion, noCache, false, outputDirectory);
}
