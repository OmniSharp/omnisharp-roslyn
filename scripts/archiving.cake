#load "runhelpers.cake"

using System.IO.Compression;
using System.Text.RegularExpressions;

/// <summary>
///  Generate the build identifier based on the RID and framework identifier.
///  Special rules when running on Travis (for publishing purposes).
/// </summary>
/// <param name="runtime">The RID</param>
/// <param name="framework">The framework identifier</param>
/// <returns>The designated build identifier</returns>
string GetBuildIdentifier(string runtime, string framework)
{
    var runtimeShort = "";
    // Default RID uses package name set in build script
    if (runtime.Equals("default"))
    {
        runtimeShort = Environment.GetEnvironmentVariable("OMNISHARP_PACKAGE_OSNAME");
    }
    else
    {
        // Remove version number
        runtimeShort = Regex.Replace(runtime, "(\\d|\\.)*-", "-");
    }

    // Rename/restrict some archive names on CI
    var travisOSName = Environment.GetEnvironmentVariable("TRAVIS_OS_NAME");
    // Travis/Linux + default + net451 is renamed to Mono
    if (string.Equals(travisOSName, "linux") && runtime.Equals("default") && framework.Equals("net451"))
    {
        return "mono";
    }
    // No need to archive other Travis + net451 combinations
    else if (travisOSName != null && framework.Equals("net451"))
    {
        return null;
    }
    // No need to archive Travis/Linux + default + not(net451) (expect all runtimes to be explicitely named)
    else if (string.Equals(travisOSName, "linux") && runtime.Equals("default") && !framework.Equals("net451"))
    {
        return null;
    }
    
    return $"{runtimeShort}-{framework}";
}

/// <summary>
///  Generate an archive out of the given published folder.
///  Use ZIP for Windows runtimes.
///  Use TAR.GZ for non-Windows runtimes.
///  Use 7z to generate TAR.GZ on Windows if available.
/// </summary>
/// <param name="runtime">The RID</param>
/// <param name="contentFolder">The folder containing the files to package</param>
/// <param name="archiveName">The target archive name (without extension)</param>
void DoArchive(string runtime, string contentFolder, string archiveName)
{
    // On all platforms use ZIP for Windows runtimes
    if (runtime.Contains("win") || (runtime.Equals("default") && IsRunningOnWindows()))
    {
        var zipFile = System.IO.Path.ChangeExtension(archiveName, "zip");
        Zip(contentFolder, zipFile);
    }
    // On all platforms use TAR.GZ for Unix runtimes
    else
    {
        var tarFile = System.IO.Path.ChangeExtension(archiveName, "tar.gz");
        // Use 7z to create TAR.GZ on Windows
        if (IsRunningOnWindows())
        {
            var tempFile = System.IO.Path.ChangeExtension(archiveName, "tar");
            try
            {
                Run("7z", $"a \"{tempFile}\"", contentFolder)
                    .ExceptionOnError($"Tar-ing failed for {contentFolder} {archiveName}");
                Run("7z", $"a \"{tarFile}\" \"{tempFile}\"", contentFolder)
                    .ExceptionOnError($"Compression failed for {contentFolder} {archiveName}");
                System.IO.File.Delete(tempFile);
            }
            catch(Win32Exception)
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

/// <summary>
///  Package a given output folder using a build identifier generated from the RID and framework identifier.
/// </summary>
/// <param name="runtime">The RID</param>
/// <param name="framework">The framework identifier</param>
/// <param name="contentFolder">The folder containing the files to package</param>
/// <param name="packageFolder">The destination folder for the archive</param>
/// <param name="projectName">The project name</param>
void Package(string runtime, string framework, string contentFolder, string packageFolder, string projectName)
{
    var buildIdentifier = GetBuildIdentifier(runtime, framework);
    if (buildIdentifier != null)
    {
        DoArchive(runtime, contentFolder, $"{packageFolder}/{projectName}-{buildIdentifier}");
    }
}