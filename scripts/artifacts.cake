#load "common.cake"
#load "runhelpers.cake"

using System.Collections.Generic;

/// <summary>
///  Generate the scripts which target the OmniSharp binaries.
/// </summary>
/// <param name="outputRoot">The root folder where the publised (or installed) binaries are located</param>
void CreateRunScript(string name, string outputRoot, string scriptFolder)
{
    CreateScript(outputRoot, scriptFolder, name);
}

private void CreateScript(string outputRoot, string scriptFolder, string name)
{
    var scriptPath = GetScriptPath(scriptFolder, name);
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

public string GetScriptPath(string scriptFolder, string name)
{
    // Tweak the script name by shaving off the "Driver" portion
    name = name.EndsWith(".Driver")
        ? name = name.Substring(0, name.Length - ".Driver".Length)
        : name;

    var result = CombinePaths(scriptFolder, name);

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
