#load "runhelpers.cake"

/// <summary>
///  Generate the scripts which target the OmniSharp binaries.
/// </summary>
/// <param name="outputRoot">The root folder where the publised (or installed) binaries are located</param>
void CreateRunScript(string outputRoot, string scriptFolder, string name)
{
    if (IsRunningOnWindows())
    {
        var desktopScript =  System.IO.Path.Combine(scriptFolder, $"{name}.cmd");
        var coreScript = System.IO.Path.Combine(scriptFolder, $"{name}.Core.cmd");
        var omniSharpPath = System.IO.Path.Combine(System.IO.Path.GetFullPath(outputRoot), "{0}", name);
        var content = new string[] {
                "SETLOCAL",
                "",
                $"\"{omniSharpPath}\" %*"
            };
        if (System.IO.File.Exists(desktopScript))
        {
            System.IO.File.Delete(desktopScript);
        }
        content[2] = String.Format(content[2], "net46");
        System.IO.File.WriteAllLines(desktopScript, content);
        if (System.IO.File.Exists(coreScript))
        {
            System.IO.File.Delete(coreScript);
        }
        content[2] = String.Format(content[2], "netcoreapp1.1");
        System.IO.File.WriteAllLines(coreScript, content);
    }
    else
    {
        var desktopScript = System.IO.Path.Combine(scriptFolder, $"{name}");
        var coreScript = System.IO.Path.Combine(scriptFolder, $"{name}.Core");
        var omniSharpPath = System.IO.Path.Combine(System.IO.Path.GetFullPath(outputRoot), "{1}", name);
        var content = new string[] {
                "#!/bin/bash",
                "",
                $"{{0}} \"{omniSharpPath}{{2}}\" \"$@\""
            };
        if (System.IO.File.Exists(desktopScript))
        {
            System.IO.File.Delete(desktopScript);
        }
        content[2] = String.Format(content[2], "mono", "net46", ".exe");
        System.IO.File.WriteAllLines(desktopScript, content);
        Run("chmod", $"+x \"{desktopScript}\"");
        if (System.IO.File.Exists(coreScript))
        {
            System.IO.File.Delete(coreScript);
        }
        content[2] = String.Format(content[2], "", "netcoreapp1.1", "");
        System.IO.File.WriteAllLines(coreScript, content);
        Run("chmod", $"+x \"{desktopScript}\"");
    }
}
