#load "runhelpers.cake"

/// <summary>
///  Generate the scripts which target the OmniSharp binaries.
/// </summary>
/// <param name="outputRoot">The root folder where the publised (or installed) binaries are located</param>
void CreateRunScript(string outputRoot, string scriptFolder, string name)
 {
    if (IsRunningOnWindows())
    {
        WriteWindowsScript(outputRoot, scriptFolder, name);
    }
    else
    {
        WriteUnixScript(outputRoot, scriptFolder, name);
    }
}

void WriteWindowsScript(string outputRoot, string scriptFolder, string name)
{
    var desktopScript =  System.IO.Path.Combine(scriptFolder, $"{name}.cmd");
    var coreScript = System.IO.Path.Combine(scriptFolder, $"{name}.Core.cmd");
    var omniSharpPath = System.IO.Path.Combine(System.IO.Path.GetFullPath(outputRoot), "{0}", "OmniSharp");
    var content = new string[] {
            "SETLOCAL",
            "",
            ""
        };

    if (System.IO.File.Exists(desktopScript))
    {
        System.IO.File.Delete(desktopScript);
    }
    content[2] = String.Format($"\"{omniSharpPath}\" %*", "net451");
    System.IO.File.WriteAllLines(desktopScript, content);

    if (System.IO.File.Exists(coreScript))
    {
        System.IO.File.Delete(coreScript);
    }
    content[2] = String.Format($"\"{omniSharpPath}\" %*", "netcoreapp1.0");
    System.IO.File.WriteAllLines(coreScript, content);
}

void WriteUnixScript(string outputRoot, string scriptFolder, string name)
{
    var desktopScript = System.IO.Path.Combine(scriptFolder, name);
    var coreScript = System.IO.Path.Combine(scriptFolder, $"{name}.Core");
    var omniSharpPath = System.IO.Path.Combine(System.IO.Path.GetFullPath(outputRoot), "{0}", "OmniSharp");
    var content = new string[] {
            "#!/bin/bash",
            "",
            ""
        };

    if (System.IO.File.Exists(desktopScript))
    {
        System.IO.File.Delete(desktopScript);
    }
    content[2] = String.Format($"{{1}} \"{omniSharpPath}{{2}}\" \"$@\"", "net451", "mono", ".exe");
    System.IO.File.WriteAllLines(desktopScript, content);
    Run("chmod", $"+x \"{desktopScript}\"");

    if (System.IO.File.Exists(coreScript))
    {
        System.IO.File.Delete(coreScript);
    }
    content[2] = String.Format($"\"{omniSharpPath}\" \"$@\"", "netcoreapp1.0");
    System.IO.File.WriteAllLines(coreScript, content);
    Run("chmod", $"+x \"{coreScript}\"");
}
