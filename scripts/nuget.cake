#load "runhelpers.cake"

ExitStatus NuGetRestore(string workingDirectory)
{
    var nugetPath = Environment.GetEnvironmentVariable("NUGET_EXE");
    var arguments = "restore";

    return IsRunningOnWindows()
        ? RunRestore(nugetPath, arguments, workingDirectory)
        : RunRestore("mono", $"\"{nugetPath}\" {arguments}", workingDirectory);
}

private ExitStatus RunNuGetInstall(string packageIdOrConfigFilePath, string version, bool excludeVersion, bool noCache, bool prerelease, string outputDirectory)
{
    var nugetPath = Environment.GetEnvironmentVariable("NUGET_EXE");
    var argList = new List<string> { "install", packageIdOrConfigFilePath };

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
