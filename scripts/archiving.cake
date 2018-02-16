#load "common.cake"
#load "runhelpers.cake"

using System.IO.Compression;

string GetPackagePrefix(string project)
{
    return project.EndsWith(".Stdio") ? string.Empty : project.Substring(project.IndexOf('.')).ToLower();
}

/// <summary>
///  Package a given output folder using a build identifier generated from the RID and framework identifier.
/// </summary>
/// <param name="platform">The platform</param>
/// <param name="contentFolder">The folder containing the files to package</param>
/// <param name="packageFolder">The destination folder for the archive</param>
/// <param name="cdFolder">The folder to drop packages into that get continously deployed to blob storage</param>
void Package(string name, string platform, string contentFolder, string packageFolder, string cdFolder)
{
    if (!DirectoryHelper.Exists(packageFolder))
    {
        DirectoryHelper.Create(packageFolder);
    }
    if (!DirectoryHelper.Exists(cdFolder))
    {
        DirectoryHelper.Create(cdFolder);
    }
    var deployFolder = $"{cdFolder}/{env.VersionInfo.SemVer}";
    if (!DirectoryHelper.Exists(deployFolder))
    {
        DirectoryHelper.Create(deployFolder);
    }

    var platformId = platform;

    if (platformId.StartsWith("win"))
    {
        var dashIndex = platformId.IndexOf("-");
        if (dashIndex >= 0)
        {
            platformId = "win-" + platformId.Substring(dashIndex + 1);
        }
    }

    var packageName = $"omnisharp{name}-{platformId}";
    var archiveName = $"{packageFolder}/{packageName}";
    var deployName = $"{deployFolder}/{packageName}";

    Information("Packaging {0}...", archiveName);

    // All platforms (Windows and Unix) produce a ZIP file
    var zipFile = $"{archiveName}.zip";
    Zip(contentFolder, zipFile);
    CopyFile(zipFile, $"{deployName}.zip");

    // Also create a TAR.GZ for Unix runtimes
    if (!platformId.StartsWith("win"))
    {
        var tarFile = $"{archiveName}.tar.gz";
        Run("tar", $"czf \"{tarFile}\" .", contentFolder)
            .ExceptionOnError($"Compression failed for {contentFolder} {archiveName}");
        CopyFile(tarFile, $"{deployName}.tar.gz");
    }
}
