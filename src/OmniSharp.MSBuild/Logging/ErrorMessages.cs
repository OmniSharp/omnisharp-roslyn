namespace OmniSharp.MSBuild.Logging
{
    internal class ErrorMessages
    {
        internal const string ReferenceAssembliesNotFoundUnix = "This project targets .NET version that requires reference assemblies that do not ship with OmniSharp out of the box (e.g. .NET Framework). The most common solution is to make sure Mono is installed on your machine (https://mono-project.com/download/) and that OmniSharp is started with that Mono installation (e.g. \"omnisharp.useGlobalMono\":\"always\" in C# Extension for VS Code).";

        internal const string ReferenceAssembliesNotFoundNet50Unix = "This project targets .NET 5.0 but the currently used MSBuild is not compatible with it - MSBuild 16.8+ is required. To solve this, run OmniSharp on its embedded Mono (e.g. 'omnisharp.useGlobalMono':'never' in C# Extension for VS Code) or, if running on global Mono installation, make sure at least Mono 6.13 is installed on your machine (https://mono-project.com/download/). Alternatively, add 'omnisharp.json' to your project root with the setting { \"msbuild\": { \"useBundledOnly\": true } }.";

        internal const string ReferenceAssembliesNotFoundNet60Unix = "This project targets .NET 6.0 but the currently used MSBuild is not compatible with it - MSBuild 16.9+ is required. To solve this, run OmniSharp on its embedded Mono (e.g. 'omnisharp.useGlobalMono':'never' in C# Extension for VS Code). Alternatively, add 'omnisharp.json' to your project root with the setting { \"msbuild\": { \"useBundledOnly\": true } }.";

        internal const string ReferenceAssembliesNotFoundNet50Windows = "This project targets .NET 5.0 but the currently used MSBuild is not compatible with it - MSBuild 16.8+ is required. To solve this, if you have Visual Studio 2019 installed on your machine, make sure it is updated to version 16.8 or add 'omnisharp.json' to your project root with the setting { \"msbuild\": { \"useBundledOnly\": true } }.";

        internal const string ReferenceAssembliesNotFoundNet60Windows = "This project targets .NET 6.0 but the currently used MSBuild is not compatible with it - MSBuild 16.9+ is required. To solve this, if you have Visual Studio 2019 installed on your machine, make sure it is updated to version 16.9 or add 'omnisharp.json' to your project root with the setting { \"msbuild\": { \"useBundledOnly\": true } }.";
    }
}
