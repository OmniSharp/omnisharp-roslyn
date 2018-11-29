using NuGet.Versioning;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OmniSharp.Services
{
    public interface IDotNetCliService
    {
        /// <summary>
        /// The path used to launch the .NET CLI. By default, this is "dotnet".
        /// </summary>
        string DotNetPath { get; }

        /// <summary>
        /// Launches "dotnet --info" in the given working directory and returns a
        /// <see cref="DotNetInfo"/> representing the returned information text.
        /// </summary>
        DotNetInfo GetInfo(string workingDirectory = null);

        /// <summary>
        /// Launches "dotnet --version" in the given working directory and returns a
        /// <see cref="SemanticVersion"/> representing the returned version text.
        /// </summary>
        SemanticVersion GetVersion(string workingDirectory = null);

        /// <summary>
        /// Launches "dotnet --version" in the given working directory and determines
        /// whether the result represents a "legacy" .NET CLI. If true, this .NET
        /// CLI supports project.json development; otherwise, it supports .csproj
        /// development.
        /// </summary>
        bool IsLegacy(string workingDirectory = null);

        /// <summary>
        /// Determines whether the specified version is from a "legacy"
        /// .NET CLI. If true, this .NET CLI supports project.json development;
        /// otherwise, it supports .csproj development.
        /// </summary>
        bool IsLegacy(SemanticVersion version);

        /// <summary>
        /// Launches "dotnet restore" in the given working directory.
        /// </summary>
        /// <param name="workingDirectory">The working directory to launch "dotnet restore" within.</param>
        /// <param name="arguments">Additional arguments to pass to "dotnet restore"</param>
        /// <param name="onFailure">A callback that will be invoked if "dotnet restore" does not
        /// return a success code.</param>
        Task RestoreAsync(string workingDirectory, string arguments = null, Action onFailure = null);

        /// <summary>
        /// Launches "dotnet" in the given working directory with the specified arguments.
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="workingDirectory"></param>
        /// <returns></returns>
        Process Start(string arguments, string workingDirectory);
    }
}
