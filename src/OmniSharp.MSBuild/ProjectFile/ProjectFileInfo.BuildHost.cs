using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using OmniSharp.MSBuild.BuildHost;

namespace OmniSharp.MSBuild.ProjectFile
{
    internal partial class ProjectFileInfo
    {
        private partial class ProjectData
        {
            public static ProjectData Create(BuildHostProject project, Guid projectGuid)
            {
                if (projectGuid == Guid.Empty)
                {
                    projectGuid = GetDeclaredProjectGuid(project.FilePath);
                }

                var projectDirectory = Path.GetDirectoryName(project.FilePath);
                var commandLine = CSharpCommandLineParser.Default.Parse(
                    project.CommandLineArgs,
                    projectDirectory,
                    RuntimeEnvironment.GetRuntimeDirectory());
                var compilationOptions = commandLine.CompilationOptions;
                var parseOptions = commandLine.ParseOptions.WithDocumentationMode(DocumentationMode.Parse);

                var metadataReferences = commandLine.MetadataReferences
                    .Select(reference => new BuildHostMetadataReference(reference.Reference, reference.Properties.Aliases))
                    .Concat(project.MetadataReferences)
                    .GroupBy(reference => reference.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new BuildHostMetadataReference(
                        group.Key,
                        group.SelectMany(reference => reference.Aliases).Distinct(StringComparer.OrdinalIgnoreCase).ToImmutableArray()))
                    .ToImmutableArray();
                var references = metadataReferences.Select(reference => reference.Path).ToImmutableArray();
                var referenceAliases = metadataReferences
                    .Where(reference => !reference.Aliases.IsDefaultOrEmpty)
                    .ToImmutableDictionary(
                        reference => reference.Path,
                        reference => string.Join(",", reference.Aliases),
                        StringComparer.OrdinalIgnoreCase);

                var projectReferences = project.ProjectReferences
                    .Where(reference => reference.ReferenceOutputAssembly)
                    .Select(reference => Path.GetFullPath(reference.Path, projectDirectory))
                    .ToImmutableArray();
                var projectReferenceAliases = project.ProjectReferences
                    .Where(reference => reference.ReferenceOutputAssembly && !reference.Aliases.IsDefaultOrEmpty)
                    .ToImmutableDictionary(
                        reference => Path.GetFullPath(reference.Path, projectDirectory),
                        reference => string.Join(",", reference.Aliases),
                        StringComparer.OrdinalIgnoreCase);

                var suppressedDiagnostics = GetDiagnosticIds(project.CommandLineArgs, "/nowarn:");
                var warningsAsErrors = GetDiagnosticIds(project.CommandLineArgs, "/warnaserror:", "/warnaserror+:");
                var warningsNotAsErrors = GetDiagnosticIds(project.CommandLineArgs, "/warnaserror-:");

                var packages = project.PackageReferences.Select(reference =>
                    new PackageReference(
                        new PackageDependency(reference.Name, PropertyConverter.ToVersionRange(reference.VersionRange)),
                        isImplicitlyDefined: false)).ToImmutableArray();

                var data = new ProjectData(
                    guid: projectGuid,
                    name: Path.GetFileNameWithoutExtension(project.FilePath),
                    assemblyName: commandLine.CompilationName ?? Path.GetFileNameWithoutExtension(project.OutputFilePath),
                    targetPath: project.OutputFilePath,
                    outputPath: GetRelativeDirectory(projectDirectory, project.OutputFilePath),
                    intermediateOutputPath: GetRelativeDirectory(projectDirectory, project.IntermediateOutputFilePath),
                    projectAssetsFile: project.ProjectAssetsFilePath,
                    configuration: project.Configuration,
                    platform: project.Platform,
                    platformTarget: GetPlatformTarget(project.CommandLineArgs),
                    targetFramework: CreateFrameworkName(project),
                    targetFrameworks: project.TargetFrameworks,
                    outputKind: compilationOptions.OutputKind,
                    languageVersion: parseOptions.LanguageVersion,
                    nullableContextOptions: compilationOptions.NullableContextOptions,
                    allowUnsafeCode: compilationOptions.AllowUnsafe,
                    checkForOverflowUnderflow: compilationOptions.CheckOverflow,
                    documentationFile: commandLine.DocumentationPath,
                    preprocessorSymbolNames: parseOptions.PreprocessorSymbolNames.ToImmutableArray(),
                    suppressedDiagnosticIds: suppressedDiagnostics,
                    warningsAsErrors: warningsAsErrors,
                    warningsNotAsErrors: warningsNotAsErrors,
                    signAssembly: !string.IsNullOrEmpty(compilationOptions.CryptoKeyFile),
                    assemblyOriginatorKeyFile: compilationOptions.CryptoKeyFile,
                    sourceFiles: project.Documents
                        .Where(document => !Path.GetFileName(document.FilePath).StartsWith("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase))
                        .Select(document => document.FilePath).ToImmutableArray(),
                    projectReferences: projectReferences,
                    references: references,
                    packageReferences: packages,
                    analyzers: commandLine.AnalyzerReferences.Select(reference => reference.FilePath).ToImmutableArray(),
                    additionalFiles: project.AdditionalDocuments.Select(document => document.FilePath).ToImmutableArray(),
                    analyzerConfigFiles: project.AnalyzerConfigDocuments.Select(document => document.FilePath).ToImmutableArray(),
                    treatWarningsAsErrors: compilationOptions.GeneralDiagnosticOption == ReportDiagnostic.Error,
                    defaultNamespace: project.DefaultNamespace,
                    runAnalyzers: !commandLine.SkipAnalyzers,
                    // BuildHost does not expose this evaluated property.
                    runAnalyzersDuringLiveAnalysis: GetBooleanProjectProperty(
                        project.FilePath,
                        PropertyNames.RunAnalyzersDuringLiveAnalysis,
                        defaultValue: true),
                    ruleset: GetRuleSet(projectDirectory, project.CommandLineArgs),
                    referenceAliases: referenceAliases,
                    projectReferenceAliases: projectReferenceAliases,
                    fileInclusionGlobs: CreateGlobs(projectDirectory, project.FileGlobs));

                data.BuildHostCompilationOptions = compilationOptions;
                data.BuildHostParseOptions = parseOptions;
                data.ProjectCapabilities = project.ProjectCapabilities;
                data.ContentFilePaths = project.ContentFilePaths;
                return data;
            }

            private static FrameworkName CreateFrameworkName(BuildHostProject project)
            {
                if (string.IsNullOrWhiteSpace(project.TargetFrameworkIdentifier) ||
                    !Version.TryParse(project.TargetFrameworkVersion?.TrimStart('v'), out var version))
                {
                    return null;
                }

                return new FrameworkName(project.TargetFrameworkIdentifier, version);
            }

            private static string GetRelativeDirectory(string projectDirectory, string filePath)
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return string.Empty;
                }

                var relativePath = Path.GetRelativePath(projectDirectory, Path.GetDirectoryName(filePath));
                return relativePath == "." ? string.Empty : relativePath + Path.DirectorySeparatorChar;
            }

            private static string GetPlatformTarget(ImmutableArray<string> arguments)
            {
                const string prefix = "/platform:";
                var argument = arguments.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                return argument == null ? string.Empty : argument.Substring(prefix.Length);
            }

            private static ImmutableArray<string> GetDiagnosticIds(
                ImmutableArray<string> arguments,
                params string[] prefixes)
            {
                return arguments.SelectMany(argument =>
                {
                    var prefix = prefixes.FirstOrDefault(value => argument.StartsWith(value, StringComparison.OrdinalIgnoreCase));
                    return prefix == null
                        ? Enumerable.Empty<string>()
                        : argument.Substring(prefix.Length).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                }).ToImmutableArray();
            }

            private static RuleSet GetRuleSet(string projectDirectory, ImmutableArray<string> arguments)
            {
                const string prefix = "/ruleset:";
                var argument = arguments.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (argument == null)
                {
                    return null;
                }

                var path = argument.Substring(prefix.Length).Trim('"');
                return RuleSet.LoadEffectiveRuleSetFromFile(Path.GetFullPath(path, projectDirectory));
            }

            private static bool GetBooleanProjectProperty(string projectFilePath, string propertyName, bool defaultValue)
            {
                var value = XDocument.Load(projectFilePath).Descendants()
                    .LastOrDefault(element =>
                        element.Name.LocalName == propertyName &&
                        element.Attribute("Condition") == null)?.Value;
                return bool.TryParse(value, out var result) ? result : defaultValue;
            }

            private static Guid GetDeclaredProjectGuid(string projectFilePath)
            {
                var value = XDocument.Load(projectFilePath).Descendants()
                    .LastOrDefault(element =>
                        element.Name.LocalName == PropertyNames.ProjectGuid &&
                        element.Attribute("Condition") == null)?.Value;
                return Guid.TryParse(value, out var projectGuid) ? projectGuid : Guid.Empty;
            }

            private static ImmutableArray<ProjectFileGlob> CreateGlobs(
                string projectDirectory,
                ImmutableArray<BuildHostFileGlob> fileGlobs)
                => fileGlobs.Select(fileGlob => new ProjectFileGlob(
                    projectDirectory,
                    fileGlob.Includes,
                    fileGlob.Excludes,
                    fileGlob.Removes)).ToImmutableArray();
        }
    }
}
