#load "common.cake"

using System.IO;
using System.Net;

void SetupMSBuild(BuildEnvironment env, BuildPlan plan)
{
    if (!Platform.Current.IsWindows)
    {
        AcquireMonoMSBuild(env, plan);
    }

    SetupMSBuildForFramework(env, "net46");
}

private void AcquireMonoMSBuild(BuildEnvironment env, BuildPlan plan)
{
    Information("Acquiring Mono MSBuild...");

    DirectoryHelper.ForceCreate(env.Folders.MonoMSBuildRuntime);
    DirectoryHelper.ForceCreate(env.Folders.MonoMSBuildLib);

    var msbuildMonoRuntimeZip = CombinePaths(env.Folders.MonoMSBuildRuntime, plan.MSBuildRuntimeForMono);
    var msbuildMonoLibZip = CombinePaths(env.Folders.MonoMSBuildLib, plan.MSBuildLibForMono);

    using (var client = new WebClient())
    {
        client.DownloadFile($"{plan.DownloadURL}/{plan.MSBuildRuntimeForMono}", msbuildMonoRuntimeZip);
        client.DownloadFile($"{plan.DownloadURL}/{plan.MSBuildLibForMono}", msbuildMonoLibZip);
    }

    Unzip(msbuildMonoRuntimeZip, env.Folders.MonoMSBuildRuntime);
    Unzip(msbuildMonoLibZip, env.Folders.MonoMSBuildLib);

    FileHelper.Delete(msbuildMonoRuntimeZip);
    FileHelper.Delete(msbuildMonoLibZip);
}

private void SetupMSBuildForFramework(BuildEnvironment env, string framework)
{
    var msbuildFolder = $"{env.Folders.MSBuildBase}-{framework}";

    if (DirectoryHelper.Exists(msbuildFolder))
    {
        DirectoryHelper.Delete(msbuildFolder, recursive: true);
    }

    if (!Platform.Current.IsWindows && framework == "net46")
    {
        Information("Copying Mono MSBuild runtime for {0}...", framework);
        DirectoryHelper.Copy(env.Folders.MonoMSBuildRuntime, msbuildFolder);
    }
    else
    {
        Information("Copying MSBuild runtime for {0}...", framework);

        var msbuildFramework = framework.StartsWith("netcoreapp")
            ? "netcoreapp1.0"
            : framework;

        var msbuildRuntimeFolder = CombinePaths(env.Folders.Tools, "Microsoft.Build.Runtime", "contentFiles", "any", msbuildFramework);
        DirectoryHelper.Copy(msbuildRuntimeFolder, msbuildFolder);
    }

    // Copy content of Microsoft.Net.Compilers
    Information("Copying Microsoft.Net.Compilers for {0}...", framework);
    var compilersFolder = CombinePaths(env.Folders.Tools, "Microsoft.Net.Compilers", "tools");
    var msbuildRoslynFolder = CombinePaths(msbuildFolder, "Roslyn");

    DirectoryHelper.Copy(compilersFolder, msbuildRoslynFolder);

    // Delete unnecessary files
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "Microsoft.CodeAnalysis.VisualBasic.dll"));
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "Microsoft.VisualBasic.Core.targets"));
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "VBCSCompiler.exe"));
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "VBCSCompiler.exe.config"));
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "vbc.exe"));
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "vbc.exe.config"));
    FileHelper.Delete(CombinePaths(msbuildRoslynFolder, "vbc.rsp"));
}