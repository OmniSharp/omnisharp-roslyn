using System.Collections.Immutable;

namespace OmniSharp.MSBuild.BuildHost
{
    internal sealed record BuildHostProject(
        string FilePath,
        string OutputFilePath,
        string IntermediateOutputFilePath,
        string DefaultNamespace,
        string TargetFramework,
        string TargetFrameworkIdentifier,
        string TargetFrameworkVersion,
        string Configuration,
        string Platform,
        ImmutableArray<string> TargetFrameworks,
        ImmutableArray<string> CommandLineArgs,
        ImmutableArray<BuildHostDocument> Documents,
        ImmutableArray<BuildHostDocument> AdditionalDocuments,
        ImmutableArray<BuildHostDocument> AnalyzerConfigDocuments,
        ImmutableArray<BuildHostProjectReference> ProjectReferences,
        ImmutableArray<string> ProjectCapabilities,
        ImmutableArray<string> ContentFilePaths,
        string ProjectAssetsFilePath,
        ImmutableArray<BuildHostPackageReference> PackageReferences,
        ImmutableArray<BuildHostMetadataReference> MetadataReferences,
        ImmutableArray<BuildHostFileGlob> FileGlobs);

    internal sealed record BuildHostDocument(string FilePath, string LogicalPath, bool IsGenerated);

    internal sealed record BuildHostProjectReference(
        string Path,
        ImmutableArray<string> Aliases,
        bool ReferenceOutputAssembly);

    internal sealed record BuildHostMetadataReference(string Path, ImmutableArray<string> Aliases);

    internal sealed record BuildHostPackageReference(string Name, string VersionRange);

    internal sealed record BuildHostFileGlob(
        ImmutableArray<string> Includes,
        ImmutableArray<string> Excludes,
        ImmutableArray<string> Removes);

    internal sealed record BuildHostDiagnostic(bool IsError, string Message, string ProjectFilePath);

    internal sealed record BuildHostLoadResult(
        BuildHostProject Project,
        ImmutableArray<BuildHostDiagnostic> Diagnostics);
}
