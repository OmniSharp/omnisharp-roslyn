#load "runhelpers.cake"

ExitStatus NuGetRestore(string workingDirectory)
{
    var nugetPath = Context.Tools.Resolve("nuget.exe");;
    var arguments = "restore";

    return IsRunningOnWindows()
        ? RunRestore(nugetPath.FullPath, arguments, workingDirectory)
        : RunRestore("mono", $"\"{nugetPath.FullPath}\" {arguments}", workingDirectory);
}