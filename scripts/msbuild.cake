#load "common.cake"

using System.Net;

void SetupMSBuild(BuildEnvironment env)
{
    var msbuildNet46Folder = env.Folders.MSBuildBase + "-net46";
    var msbuildNetCoreAppFolder = env.Folders.MSBuildBase + "-netcoreapp1.1";

    if (!IsRunningOnWindows())
    {
        if (DirectoryExists(env.Folders.MonoMSBuildRuntime))
        {
            DeleteDirectory(env.Folders.MonoMSBuildRuntime, recursive: true);
        }

        if (DirectoryExists(env.Folders.MonoMSBuildLib))
        {
            DeleteDirectory(env.Folders.MonoMSBuildLib, recursive: true);
        }

        CreateDirectory(env.Folders.MonoMSBuildRuntime);
        CreateDirectory(env.Folders.MonoMSBuildLib);

        var msbuildMonoRuntimeZip = CombinePaths(env.Folders.MonoMSBuildRuntime, buildPlan.MSBuildRuntimeForMono);
        var msbuildMonoLibZip = CombinePaths(env.Folders.MonoMSBuildLib, buildPlan.MSBuildLibForMono);

        using (var client = new WebClient())
        {
            client.DownloadFile($"{buildPlan.DownloadURL}/{buildPlan.MSBuildRuntimeForMono}", msbuildMonoRuntimeZip);
            client.DownloadFile($"{buildPlan.DownloadURL}/{buildPlan.MSBuildLibForMono}", msbuildMonoLibZip);
        }

        Unzip(msbuildMonoRuntimeZip, env.Folders.MonoMSBuildRuntime);
        Unzip(msbuildMonoLibZip, env.Folders.MonoMSBuildLib);

        DeleteFile(msbuildMonoRuntimeZip);
        DeleteFile(msbuildMonoLibZip);
    }

    if (DirectoryExists(msbuildNet46Folder))
    {
        DeleteDirectory(msbuildNet46Folder, recursive: true);
    }

    if (DirectoryExists(msbuildNetCoreAppFolder))
    {
        DeleteDirectory(msbuildNetCoreAppFolder, recursive: true);
    }

    CreateDirectory(msbuildNet46Folder);
    CreateDirectory(msbuildNetCoreAppFolder);

    // Copy MSBuild runtime to appropriate locations
    var msbuildInstallFolder = CombinePaths(env.Folders.Tools, "Microsoft.Build.Runtime", "contentFiles", "any");
    var msbuildNet46InstallFolder = CombinePaths(msbuildInstallFolder, "net46");
    var msbuildNetCoreAppInstallFolder = CombinePaths(msbuildInstallFolder, "netcoreapp1.0");

    if (IsRunningOnWindows())
    {
        CopyDirectory(msbuildNet46InstallFolder, msbuildNet46Folder);
    }
    else
    {
        CopyDirectory(env.Folders.MonoMSBuildRuntime, msbuildNet46Folder);
    }

    CopyDirectory(msbuildNetCoreAppInstallFolder, msbuildNetCoreAppFolder);

    // Finally, copy Microsoft.Net.Compilers
    var roslynFolder = CombinePaths(env.Folders.Tools, "Microsoft.Net.Compilers", "tools");
    var roslynNet46Folder = CombinePaths(msbuildNet46Folder, "Roslyn");
    var roslynNetCoreAppFolder = CombinePaths(msbuildNetCoreAppFolder, "Roslyn");

    CreateDirectory(roslynNet46Folder);
    CreateDirectory(roslynNetCoreAppFolder);

    CopyDirectory(roslynFolder, roslynNet46Folder);
    CopyDirectory(roslynFolder, roslynNetCoreAppFolder);

    // Delete unnecessary files
    foreach (var folder in new[] { roslynNet46Folder, roslynNetCoreAppFolder })
    {
        DeleteFile(CombinePaths(folder, "Microsoft.CodeAnalysis.VisualBasic.dll"));
        DeleteFile(CombinePaths(folder, "Microsoft.VisualBasic.Core.targets"));
        DeleteFile(CombinePaths(folder, "VBCSCompiler.exe"));
        DeleteFile(CombinePaths(folder, "VBCSCompiler.exe.config"));
        DeleteFile(CombinePaths(folder, "vbc.exe"));
        DeleteFile(CombinePaths(folder, "vbc.exe.config"));
        DeleteFile(CombinePaths(folder, "vbc.rsp"));
    }
}