namespace OmniSharp.MSBuild.Logging
{
    internal class ErrorMessages
    {
        internal const string ReferenceAssembliesNotFoundUnix = "This project targets .NET version that requires reference assemblies that do not ship with OmniSharp out of the box (e.g. .NET Framework). The most common solution is to make sure Mono is installed on your machine (https://mono-project.com/download/) and that OmniSharp is started with that Mono installation (e.g. 'omnisharp.useGlobalMono':'always' in C# Extension for VS Code).";
    }
}
