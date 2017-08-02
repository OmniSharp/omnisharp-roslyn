#load "common.cake"

using System.IO;
using System.Net;

void SetupMSBuild(BuildEnvironment env, BuildPlan plan)
{
    SetupMSBuildForFramework(env, "net46");
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