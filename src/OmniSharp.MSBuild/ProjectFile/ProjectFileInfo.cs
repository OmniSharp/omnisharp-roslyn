using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp.MSBuild.Logging;
using OmniSharp.MSBuild.Notification;

namespace OmniSharp.MSBuild.ProjectFile
{
    internal partial class ProjectFileInfo
    {
        private readonly ProjectData _data;

        public string FilePath { get; }
        public string Directory { get; }

        public ProjectId Id { get => ProjectIdInfo.Id; }

        public Guid Guid => _data.Guid;
        public string Name => _data.Name;

        public string AssemblyName => _data.AssemblyName;
        public string TargetPath => _data.TargetPath;
        public string OutputPath => _data.OutputPath;
        public string IntermediateOutputPath => _data.IntermediateOutputPath;
        public string ProjectAssetsFile => _data.ProjectAssetsFile;

        public string Configuration => _data.Configuration;
        public string Platform => _data.Platform;
        public FrameworkName TargetFramework => _data.TargetFramework;
        public ImmutableArray<string> TargetFrameworks => _data.TargetFrameworks;

        public OutputKind OutputKind => _data.OutputKind;
        public LanguageVersion LanguageVersion => _data.LanguageVersion;
        public NullableContextOptions NullableContextOptions => _data.NullableContextOptions;
        public bool AllowUnsafeCode => _data.AllowUnsafeCode;
        public string DocumentationFile => _data.DocumentationFile;
        public ImmutableArray<string> PreprocessorSymbolNames => _data.PreprocessorSymbolNames;
        public ImmutableArray<string> SuppressedDiagnosticIds => _data.SuppressedDiagnosticIds;

        public bool SignAssembly => _data.SignAssembly;
        public string AssemblyOriginatorKeyFile => _data.AssemblyOriginatorKeyFile;
        public RuleSet RuleSet => _data.RuleSet;

        public ImmutableArray<string> SourceFiles => _data.SourceFiles;
        public ImmutableArray<string> References => _data.References;
        public ImmutableArray<string> ProjectReferences => _data.ProjectReferences;
        public ImmutableArray<PackageReference> PackageReferences => _data.PackageReferences;
        public ImmutableArray<string> Analyzers => _data.Analyzers;
        public ImmutableDictionary<string, string> ReferenceAliases => _data.ReferenceAliases;
        public bool TreatWarningsAsErrors => _data.TreatWarningsAsErrors;
        public ProjectIdInfo ProjectIdInfo { get; }

        private ProjectFileInfo(
            ProjectIdInfo projectIdInfo,
            string filePath,
            ProjectData data)
        {
            this.ProjectIdInfo = projectIdInfo;
            this.FilePath = filePath;
            this.Directory = Path.GetDirectoryName(filePath);

            _data = data;
        }

        internal static ProjectFileInfo CreateEmpty(string filePath)
        {
            var id = ProjectId.CreateNewId(debugName: filePath);

            return new ProjectFileInfo(new ProjectIdInfo(id, isDefinedInSolution:false), filePath, data: null);
        }

        internal static ProjectFileInfo CreateNoBuild(string filePath, ProjectLoader loader)
        {
            var id = ProjectId.CreateNewId(debugName: filePath);
            var project = loader.EvaluateProjectFile(filePath);
            var data = ProjectData.Create(project);
            //we are not reading the solution here 
            var projectIdInfo = new ProjectIdInfo(id, isDefinedInSolution: false);

            return new ProjectFileInfo(projectIdInfo, filePath, data);
        }

        public static (ProjectFileInfo, ImmutableArray<MSBuildDiagnostic>, ProjectLoadedEventArgs) Load(string filePath, ProjectIdInfo projectIdInfo, ProjectLoader loader)
        {
            if (!File.Exists(filePath))
            {
                return (null, ImmutableArray<MSBuildDiagnostic>.Empty, null);
            }

            var (projectInstance, diagnostics) = loader.BuildProject(filePath);
            if (projectInstance == null)
            {
                return (null, diagnostics, null);
            }

            var data = ProjectData.Create(projectInstance);
            var projectFileInfo = new ProjectFileInfo(projectIdInfo, filePath, data);
            var eventArgs = new ProjectLoadedEventArgs(projectIdInfo.Id,
                                                       projectInstance,
                                                       diagnostics,
                                                       isReload: false,
                                                       projectIdInfo.IsDefinedInSolution,
                                                       projectFileInfo.SourceFiles,
                                                       data.References);

            return (projectFileInfo, diagnostics, eventArgs);
        }

        public (ProjectFileInfo, ImmutableArray<MSBuildDiagnostic>, ProjectLoadedEventArgs) Reload(ProjectLoader loader)
        {
            var (projectInstance, diagnostics) = loader.BuildProject(FilePath);
            if (projectInstance == null)
            {
                return (null, diagnostics, null);
            }

            var data = ProjectData.Create(projectInstance);
            var projectFileInfo = new ProjectFileInfo(ProjectIdInfo, FilePath, data);
            var eventArgs = new ProjectLoadedEventArgs(Id, projectInstance, diagnostics, isReload: true, ProjectIdInfo.IsDefinedInSolution,data.References);

            return (projectFileInfo, diagnostics, eventArgs);
        }

        public bool IsUnityProject()
            => References.Any(filePath =>
                {
                    var fileName = Path.GetFileName(filePath);

                    return string.Equals(fileName, "UnityEngine.dll", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(fileName, "UnityEditor.dll", StringComparison.OrdinalIgnoreCase);
                });
    }
}
