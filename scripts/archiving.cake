#load "common.cake"
#load "runhelpers.cake"

using System.IO.Compression;

/// <summary>
///  Package a given output folder using a build identifier generated from the RID and framework identifier.
/// </summary>
/// <param name="platform">The platform</param>
/// <param name="contentFolder">The folder containing the files to package</param>
/// <param name="packageFolder">The destination folder for the archive</param>
/// <param name="projectName">The project name</param>
void Package(string platform, string contentFolder, string packageFolder)
{
    if (!DirectoryHelper.Exists(packageFolder))
    {
        DirectoryHelper.Create(packageFolder);
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

    var archiveName = $"{packageFolder}/omnisharp-{platformId}";

    Information("Packaging {0}...", archiveName);

    // On all platforms use ZIP for Windows runtimes
    if (platformId.StartsWith("win"))
    {
        var zipFile = $"{archiveName}.zip";
        Zip(contentFolder, zipFile);
    }
    // On all platforms use TAR.GZ for Unix runtimes
    else
    {
        var tarFile = $"{archiveName}.tar.gz";
        // Use 7z to create TAR.GZ on Windows
        if (Platform.Current.IsWindows)
        {
            var tempFile = $"{archiveName}.tar";
            try
            {
                Run("7z", $"a \"{tempFile}\"", contentFolder)
                    .ExceptionOnError($"Tar-ing failed for {contentFolder} {archiveName}");
                Run("7z", $"a \"{tarFile}\" \"{tempFile}\"", contentFolder)
                    .ExceptionOnError($"Compression failed for {contentFolder} {archiveName}");
                    
                FileHelper.Delete(tempFile);
            }
            catch (Win32Exception)
            {
                Information("Warning: 7z not available on PATH to pack tar.gz results");
            }
        }
        // Use tar to create TAR.GZ on Unix
        else
        {
            Run("tar", $"czf \"{tarFile}\" .", contentFolder)
                .ExceptionOnError($"Compression failed for {contentFolder} {archiveName}");
        }
    }
}