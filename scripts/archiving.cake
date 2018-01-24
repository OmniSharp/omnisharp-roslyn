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

    var platformId = platform;

    if (platformId.StartsWith("win"))
    {
        var dashIndex = platformId.IndexOf("-");
        if (dashIndex >= 0)
        {
            platformId = "win-" + platformId.Substring(dashIndex + 1);
        }
    }

    var archiveName = $"{packageFolder}/omnisharp{name}-{platformId}";
    var deployArchiveName = archiveName.Replace(packageFolder, cdFolder);

    Information("Packaging {0}...", archiveName);

    // On all platforms use ZIP for Windows runtimes
    if (platformId.StartsWith("win"))
    {
        var zipFile = $"{archiveName}.zip";
        Zip(contentFolder, zipFile);
        CopyFile(zipFile, $"{deployArchiveName}.{env.VersionInfo.SemVer}.zip");
    }
    // On all platforms use TAR.GZ for Unix runtimes
    else
    {
        var tarFile = $"{archiveName}.tar.gz";
        Run("tar", $"czf \"{tarFile}\" .", contentFolder)
            .ExceptionOnError($"Compression failed for {contentFolder} {archiveName}");
        CopyFile(tarFile, $"{deployArchiveName}.{env.VersionInfo.SemVer}.tar.gz");
    }
}
