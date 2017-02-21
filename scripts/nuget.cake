#load "runhelpers.cake"

ExitStatus NuGetRestore(string workingDirectory)
{
    var nugetPath = Environment.GetEnvironmentVariable("NUGET_EXE");
    var arguments = "restore";

    return IsRunningOnWindows()
        ? RunRestore(nugetPath, arguments, workingDirectory)
        : RunRestore("mono", $"\"{nugetPath}\" {arguments}", workingDirectory);
}