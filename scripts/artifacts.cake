#load "runhelpers.cake"

/// <summary>
///  Generate the scripts which target the OmniSharp binaries.
/// </summary>
/// <param name="outputRoot">The root folder where the publised (or installed) binaries are located</param>
void CreateRunScript(string outputRoot, string scriptFolder)
 {
    if (IsRunningOnWindows())
    {
        WriteWindowsScript(outputRoot, scriptFolder);
    }
    else
    {
        WriteUnixScript(outputRoot, scriptFolder);
    }
}

void WriteWindowsScript(string outputRoot, string scriptFolder)
{
    var desktopScript = System.IO.Path.Combine(scriptFolder, $"OmniSharp.cmd");
    var omniSharpPath = System.IO.Path.Combine(System.IO.Path.GetFullPath(outputRoot), "net46", "OmniSharp");

    var coreScript = System.IO.Path.Combine(scriptFolder, $"OmniSharp.Core.cmd");
    var omniSharpCorePath = System.IO.Path.Combine(System.IO.Path.GetFullPath(outputRoot), "netcoreapp1.0", "OmniSharp");

    var content = new string[] {
            "SETLOCAL",
            "",
            ""
        };

    if (System.IO.File.Exists(desktopScript))
    {
        System.IO.File.Delete(desktopScript);
    }
    content[2] = $"\"{omniSharpPath}\" %*";
    System.IO.File.WriteAllLines(desktopScript, content);

    if (System.IO.File.Exists(coreScript))
    {
        System.IO.File.Delete(coreScript);
    }
    content[2] = $"\"{omniSharpCorePath}\" %*";
    System.IO.File.WriteAllLines(coreScript, content);
}

void WriteUnixScript(string outputRoot, string scriptFolder)
{
    var desktopScript = System.IO.Path.Combine(scriptFolder, "OmniSharp");
    var omniSharpPath = System.IO.Path.Combine(System.IO.Path.GetFullPath(outputRoot), "net46", "OmniSharp");

    var coreScript = System.IO.Path.Combine(scriptFolder, $"OmniSharp.Core");
    var omniSharpCorePath = System.IO.Path.Combine(System.IO.Path.GetFullPath(outputRoot), "netcoreapp1.0", "OmniSharp");

    var content = new string[] {
            "#!/bin/bash",
            "",
            ""
        };

    if (System.IO.File.Exists(desktopScript))
    {
        System.IO.File.Delete(desktopScript);
    }
    content[2] = $"mono \"{omniSharpPath}.exe\" \"$@\"";
    System.IO.File.WriteAllLines(desktopScript, content);
    Run("chmod", $"+x \"{desktopScript}\"");

    if (System.IO.File.Exists(coreScript))
    {
        System.IO.File.Delete(coreScript);
    }
    content[2] = $"\"{omniSharpCorePath}\" \"$@\"";
    System.IO.File.WriteAllLines(coreScript, content);
    Run("chmod", $"+x \"{coreScript}\"");
}