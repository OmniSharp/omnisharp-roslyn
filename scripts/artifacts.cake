#load "common.cake"
#load "runhelpers.cake"

using System.Collections.Generic;

/// <summary>
///  Generate the scripts which target the OmniSharp binaries.
/// </summary>
/// <param name="outputRoot">The root folder where the publised (or installed) binaries are located</param>
void CreateRunScript(string outputRoot, string scriptFolder)
{
    CreateScript(outputRoot, scriptFolder);
}

private void CreateScript(string outputRoot, string scriptFolder)
{
    var scriptPath = GetScriptPath(scriptFolder);
    var omniSharpPath = GetOmniSharpPath(outputRoot);
    var content = GetScriptContent(omniSharpPath);

    if (FileHelper.Exists(scriptPath))
    {
        FileHelper.Delete(scriptPath);
    }

    FileHelper.WriteAllLines(scriptPath, content);

    if (!Platform.Current.IsWindows)
    {
        Run("chmod", $"+x \"{scriptPath}\"");
    }
}

private string GetScriptPath(string scriptFolder)
{
    var result = CombinePaths(scriptFolder, "OmniSharp");

    if (Platform.Current.IsWindows)
    {
        result += ".cmd";
    }

    return result;
}

private string GetOmniSharpPath(string outputRoot)
{
    return CombinePaths(PathHelper.GetFullPath(outputRoot), "OmniSharp.exe");
}

private string[] GetScriptContent(string omniSharpPath)
{
    var lines = new List<string>();

    if (Platform.Current.IsWindows)
    {
        lines.Add("SETLOCAL");
    }
    else
    {
        lines.Add("#!/bin/bash");
    }

    lines.Add("");

    var arguments = Platform.Current.IsWindows
        ? "%*"
        : "\"$@\"";

    if (Platform.Current.IsWindows)
    {
        lines.Add($"\"{omniSharpPath}\" {arguments}");
    }
    else
    {
        lines.Add($"mono --assembly-loader=strict \"{omniSharpPath}\" {arguments}");
    }

    return lines.ToArray();
}
