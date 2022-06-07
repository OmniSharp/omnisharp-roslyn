namespace OmniSharp.MSBuild.Logging
{
    internal class ErrorMessages
    {
        internal const string ReferenceAssembliesNotFoundUnix = "This project targets .NET version that requires reference assemblies that are not installed (e.g. .NET Framework). The most common solution is to make sure Mono is fully updated on your machine (https://mono-project.com/download/) and that you are running the .NET Framework build of OmniSharp (e.g. 'omnisharp.useModernNet': false in C# Extension for VS Code).";

        internal const string ReferenceAssembliesNotFoundNet50Unix = "This project targets .NET 5.0 but the currently used MSBuild is not compatible with it - MSBuild 16.8+ is required. To solve this, run the net6.0 build of OmniSharp on the .NET SDK. (e.g. 'omnisharp.useModernNet': true in C# Extension for VS Code).";

        internal const string ReferenceAssembliesNotFoundNet60Unix = "This project targets .NET 6.0 but the currently used MSBuild is not compatible with it - MSBuild 17.0+ is required. To solve this, run the net6.0 build of OmniSharp on the .NET SDK. (e.g. 'omnisharp.useModernNet': true in C# Extension for VS Code).";

        internal const string ReferenceAssembliesNotFoundNet50Windows = "This project targets .NET 5.0 but the currently used MSBuild is not compatible with it - MSBuild 16.8+ is required. To solve this, if you have Visual Studio 2019 installed on your machine, make sure it is updated to version 16.8.";

        internal const string ReferenceAssembliesNotFoundNet60Windows = "This project targets .NET 6.0 but the currently used MSBuild is not compatible with it - MSBuild 17.0+ is required. To solve this, if you have Visual Studio 2022 installed on your machine, make sure it is updated to version 17.0.";
    }
}
