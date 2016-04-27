#load "runhelpers.cake"

/// <summary>
///  Generate the scripts which target the OmniSharp binaries.
/// </summary>
/// <param name="outputRoot">The root folder where the publised (or installed) binaries are located</param>
void CreateRunScript(string outputRoot, string scriptFolder)
{
    if (IsRunningOnWindows())
    {
        var desktopScript =  System.IO.Path.Combine(scriptFolder, "OmniSharp.cmd");
        var coreScript = System.IO.Path.Combine(scriptFolder, "OmniSharp.Core.cmd");
        var omniSharpPath = System.IO.Path.Combine(System.IO.Path.GetFullPath(outputRoot), "{0}", "OmniSharp");
        var content = new string[] {
                "SETLOCAL",
                "",
                $"\"{omniSharpPath}\" %*"
            };
        if (System.IO.File.Exists(desktopScript))
        {
            System.IO.File.Delete(desktopScript);
        }
        content[2] = String.Format(content[2], "net451");
        System.IO.File.WriteAllLines(desktopScript, content);
        if (System.IO.File.Exists(coreScript))
        {
            System.IO.File.Delete(coreScript);
        }
        content[2] = String.Format(content[2], "netcoreapp1.0");
        System.IO.File.WriteAllLines(coreScript, content);
    }
    else
    {
        var desktopScript = System.IO.Path.Combine(scriptFolder, "OmniSharp");
        var coreScript = System.IO.Path.Combine(scriptFolder, "OmniSharp.Core");
        var omniSharpPath = System.IO.Path.Combine(System.IO.Path.GetFullPath(outputRoot), "{1}", "OmniSharp");
        var content = new string[] {
                "#!/bin/bash",
                "",
                $"{{0}} \"{omniSharpPath}{{2}}\" \"$@\""
            };
        if (System.IO.File.Exists(desktopScript))
        {
            System.IO.File.Delete(desktopScript);
        }
        content[2] = String.Format(content[2], "mono", "net451", ".exe");
        System.IO.File.WriteAllLines(desktopScript, content);
        Run("chmod", $"+x \"{desktopScript}\"");
        if (System.IO.File.Exists(coreScript))
        {
            System.IO.File.Delete(coreScript);
        }
        content[2] = String.Format(content[2], "", "netcoreapp1.0", "");
        System.IO.File.WriteAllLines(coreScript, content);
        Run("chmod", $"+x \"{desktopScript}\"");
    }
}