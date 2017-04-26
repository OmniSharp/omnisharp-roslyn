using System;
using System.Collections.Immutable;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OmniSharp.MSBuild.ProjectFile
{
    public partial class ProjectFileInfo
    {
        private class ProjectData
        {
            public Guid Guid { get; }
            public string Name { get; }

            public string AssemblyName { get; }
            public string TargetPath { get; }
            public string OutputPath { get;  }
            public string ProjectAssetsFile { get; }

            public FrameworkName TargetFramework { get; }
            public ImmutableArray<string> TargetFrameworks { get; }

            public OutputKind OutputKind { get; }
            public LanguageVersion LanguageVersion { get; }
            public bool AllowUnsafeCode { get; }
            public string DocumentationFile { get; }
            public ImmutableArray<string> PreprocessorSymbolNames { get; }
            public ImmutableArray<string> SuppressedDiagnosticIds { get; }

            public bool SignAssembly { get; }
            public string AssemblyOriginatorKeyFile { get; }

            public ImmutableArray<string> SourceFiles { get; }
            public ImmutableArray<string> ProjectReferences { get; }
            public ImmutableArray<string> References { get; }
            public ImmutableArray<PackageReference> PackageReferences { get; }
            public ImmutableArray<string> Analyzers { get; }

            public ProjectData(
                Guid guid, string name,
                string assemblyName, string targetPath, string outputPath, string projectAssetsFile,
                FrameworkName targetFramework,
                ImmutableArray<string> targetFrameworks,
                OutputKind outputKind,
                LanguageVersion languageVersion,
                bool allowUnsafeCode,
                string documentationFile,
                ImmutableArray<string> preprocessorSymbolNames,
                ImmutableArray<string> suppressedDiagnosticIds,
                bool signAssembly,
                string assemblyOriginatorKeyFile,
                ImmutableArray<string> sourceFiles,
                ImmutableArray<string> projectReferences,
                ImmutableArray<string> references,
                ImmutableArray<PackageReference> packageReferences,
                ImmutableArray<string> analyzers)
            {
                Guid = guid;
                Name = name;

                AssemblyName = assemblyName;
                TargetPath = targetPath;
                OutputPath = outputPath;
                ProjectAssetsFile = projectAssetsFile;

                TargetFramework = targetFramework;
                TargetFrameworks = targetFrameworks;

                OutputKind = outputKind;
                LanguageVersion = LanguageVersion;
                AllowUnsafeCode = allowUnsafeCode;
                DocumentationFile = documentationFile;
                PreprocessorSymbolNames = preprocessorSymbolNames;
                SuppressedDiagnosticIds = suppressedDiagnosticIds;

                SignAssembly = signAssembly;
                AssemblyOriginatorKeyFile = assemblyOriginatorKeyFile;

                SourceFiles = sourceFiles;
                ProjectReferences = projectReferences;
                References = references;
                PackageReferences = packageReferences;
                Analyzers = analyzers;
            }
        }
    }
}
