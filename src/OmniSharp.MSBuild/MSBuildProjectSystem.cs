using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using OmniSharp.MSBuild.Analyzers;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Options;
using OmniSharp.Services;

namespace OmniSharp.MSBuild
{
    [Export(typeof(IProjectSystem))]
    public class MSBuildProjectSystem : IProjectSystem
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IMetadataFileReferenceCache _metadataReferenceCache;
        private readonly IOmnisharpEnvironment _env;
        private readonly ILogger _logger;
        private readonly IEventEmitter _emitter;
        private static readonly Guid[] _supportsProjectTypes = new[] {
            new Guid("fae04ec0-301f-11d3-bf4b-00c04f79efbc") // CSharp
        };

        private readonly MSBuildContext _context;
        private readonly IFileSystemWatcher _watcher;

        private MSBuildOptions _options;

        [ImportingConstructor]
        public MSBuildProjectSystem(OmnisharpWorkspace workspace,
                                    IOmnisharpEnvironment env,
                                    ILoggerFactory loggerFactory,
                                    IEventEmitter emitter,
                                    IMetadataFileReferenceCache metadataReferenceCache,
                                    IFileSystemWatcher watcher,
                                    MSBuildContext context)
        {
            _workspace = workspace;
            _metadataReferenceCache = metadataReferenceCache;
            _watcher = watcher;
            _env = env;
            _logger = loggerFactory.CreateLogger<MSBuildProjectSystem>();
            _emitter = emitter;
            _context = context;
        }

        public string Key { get { return "MsBuild"; } }
        public string Language { get { return LanguageNames.CSharp; } }
        public IEnumerable<string> Extensions { get; } = new[] { ".cs" };

        public void Initalize(IConfiguration configuration)
        {
            _options = new MSBuildOptions();
            ConfigurationBinder.Bind(configuration, _options);

            var solutionFilePath = _env.SolutionFilePath;

            if (string.IsNullOrEmpty(solutionFilePath))
            {
                var solutions = Directory.GetFiles(_env.Path, "*.sln");
                var result = SolutionPicker.ChooseSolution(_env.Path, solutions);

                if (result.Message != null)
                {
                    _logger.LogInformation(result.Message);
                }

                if (result.Solution == null)
                {
                    return;
                }

                solutionFilePath = result.Solution;
            }

            SolutionFile solutionFile = null;

            _context.SolutionPath = solutionFilePath;

            using (var stream = File.OpenRead(solutionFilePath))
            {
                using (var reader = new StreamReader(stream))
                {
                    solutionFile = SolutionFile.Parse(reader);
                }
            }
            _logger.LogInformation($"Detecting projects in '{solutionFilePath}'.");

            foreach (var block in solutionFile.ProjectBlocks)
            {
                if (!_supportsProjectTypes.Contains(block.ProjectTypeGuid))
                {
                    if (UnityTypeGuid(block.ProjectName) != block.ProjectTypeGuid)
                    {
                        _logger.LogWarning("Skipped unsupported project type '{0}'", block.ProjectPath);
                        continue;
                    }
                }

                if (_context.ProjectGuidToWorkspaceMapping.ContainsKey(block.ProjectGuid))
                {
                    continue;
                }

                var projectFilePath = Path.GetFullPath(Path.GetFullPath(Path.Combine(_env.Path, block.ProjectPath.Replace('\\', Path.DirectorySeparatorChar))));

                _logger.LogInformation($"Loading project from '{projectFilePath}'.");

                var projectFileInfo = CreateProject(projectFilePath);

                if (projectFileInfo == null)
                {
                    continue;
                }

                var compilationOptions = new CSharpCompilationOptions(projectFileInfo.OutputKind);
#if DNX451
                compilationOptions = compilationOptions.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default);
#endif

                if (projectFileInfo.AllowUnsafe)
                {
                    compilationOptions = compilationOptions.WithAllowUnsafe(true);
                }

                var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(projectFileInfo.Name),
                                                     VersionStamp.Create(),
                                                     projectFileInfo.Name,
                                                     projectFileInfo.AssemblyName,
                                                     LanguageNames.CSharp,
                                                     projectFileInfo.ProjectFilePath,
                                                     compilationOptions: compilationOptions);

                _workspace.AddProject(projectInfo);

                projectFileInfo.WorkspaceId = projectInfo.Id;

                _context.Projects[projectFileInfo.ProjectFilePath] = projectFileInfo;
                _context.ProjectGuidToWorkspaceMapping[block.ProjectGuid] = projectInfo.Id;

                _watcher.Watch(projectFilePath, OnProjectChanged);
            }

            foreach (var projectFileInfo in _context.Projects.Values)
            {
                UpdateProject(projectFileInfo);
            }

        }

        public static Guid UnityTypeGuid(string projectName)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(projectName);
                var hash = md5.ComputeHash(bytes);

                var bigEndianHash = new[] {
                    hash[3], hash[2], hash[1], hash[0],
                    hash[5], hash[4],
                    hash[7], hash[6],
                    hash[8], hash[9], hash[10], hash[11], hash[12], hash[13], hash[14], hash[15]
                };

                return new System.Guid(bigEndianHash);
            }
        }

        private ProjectFileInfo CreateProject(string projectFilePath)
        {
            ProjectFileInfo projectFileInfo = null;
            var diagnostics = new List<MSBuildDiagnosticsMessage>();

            try
            {
                projectFileInfo = ProjectFileInfo.Create(_options, _logger, _env.Path, projectFilePath, diagnostics);

                if (projectFileInfo == null)
                {
                    _logger.LogWarning($"Failed to process project file '{projectFilePath}'.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to process project file '{projectFilePath}'.", ex);
                _emitter.Emit(EventTypes.Error, new ErrorMessage()
                {
                    FileName = projectFilePath,
                    Text = ex.ToString()
                });
            }

            _emitter.Emit(EventTypes.MsBuildProjectDiagnostics, new MSBuildProjectDiagnostics()
            {
                FileName = projectFilePath,
                Warnings = diagnostics.Where(d => d.LogLevel == "Warning"),
                Errors = diagnostics.Where(d => d.LogLevel == "Error"),
            });

            return projectFileInfo;
        }

        private void OnProjectChanged(string projectFilePath)
        {
            var newProjectInfo = CreateProject(projectFilePath);

            // Should we remove the entry if the project is malformed?
            if (newProjectInfo != null)
            {
                lock (_context)
                {
                    ProjectFileInfo oldProjectFileInfo;
                    if (_context.Projects.TryGetValue(projectFilePath, out oldProjectFileInfo))
                    {
                        _context.Projects[projectFilePath] = newProjectInfo;
                        newProjectInfo.WorkspaceId = oldProjectFileInfo.WorkspaceId;
                        UpdateProject(newProjectInfo);
                    }
                }
            }
        }

        private void UpdateProject(ProjectFileInfo projectFileInfo)
        {
            var project = _workspace.CurrentSolution.GetProject(projectFileInfo.WorkspaceId);

            var unusedDocuments = project.Documents.ToDictionary(d => d.FilePath, d => d.Id);

            foreach (var file in projectFileInfo.SourceFiles)
            {
                if (unusedDocuments.Remove(file))
                {
                    continue;
                }

                using (var stream = File.OpenRead(file))
                {
                    var sourceText = SourceText.From(stream, encoding: Encoding.UTF8);
                    var id = DocumentId.CreateNewId(projectFileInfo.WorkspaceId);
                    var version = VersionStamp.Create();

                    var loader = TextLoader.From(TextAndVersion.Create(sourceText, version));

                    _workspace.AddDocument(DocumentInfo.Create(id, file, filePath: file, loader: loader));
                }
            }

            if (projectFileInfo.SpecifiedLanguageVersion.HasValue || projectFileInfo.DefineConstants != null)
            {
                var parseOptions = projectFileInfo.SpecifiedLanguageVersion.HasValue
                    ? new CSharpParseOptions(projectFileInfo.SpecifiedLanguageVersion.Value)
                    : new CSharpParseOptions();
                if (projectFileInfo.DefineConstants != null && projectFileInfo.DefineConstants.Any())
                {
                    parseOptions = parseOptions.WithPreprocessorSymbols(projectFileInfo.DefineConstants);
                }
                _workspace.SetParseOptions(project.Id, parseOptions);
            }

            foreach (var unused in unusedDocuments)
            {
                _workspace.RemoveDocument(unused.Value);
            }

            var unusedProjectReferences = new HashSet<ProjectReference>(project.ProjectReferences);

            foreach (var projectReferencePath in projectFileInfo.ProjectReferences)
            {
                ProjectFileInfo projectReferenceInfo;
                if (_context.Projects.TryGetValue(projectReferencePath, out projectReferenceInfo))
                {
                    var reference = new ProjectReference(projectReferenceInfo.WorkspaceId);

                    if (unusedProjectReferences.Remove(reference))
                    {
                        // This reference already exists
                        continue;
                    }

                    _workspace.AddProjectReference(project.Id, reference);
                }
                else
                {
                    _logger.LogWarning($"Unable to resolve project reference '{projectReferencePath}' for '{projectFileInfo}'.");
                }
            }

            foreach (var unused in unusedProjectReferences)
            {
                _workspace.RemoveProjectReference(project.Id, unused);
            }

            var unusedAnalyzers = new Dictionary<string, AnalyzerReference>(project.AnalyzerReferences.ToDictionary(a => a.FullPath));

            foreach (var analyzerPath in projectFileInfo.Analyzers)
            {
                if (!File.Exists(analyzerPath))
                {
                    _logger.LogWarning($"Unable to resolve assembly '{analyzerPath}'");
                }
                else
                {
                    if (unusedAnalyzers.Remove(analyzerPath))
                    {
                        continue;
                    }
#if DNX451
                    var analyzerReference = new AnalyzerFileReference(analyzerPath, new SimpleAnalyzerAssemblyLoader());
                    project.AddAnalyzerReference(analyzerReference);
#endif
                }
            }

            foreach (var analyzerReference in unusedAnalyzers.Values)
            {
                project.RemoveAnalyzerReference(analyzerReference);
            }

            var unusedReferences = new HashSet<MetadataReference>(project.MetadataReferences);

            foreach (var referencePath in projectFileInfo.References)
            {
                if (!File.Exists(referencePath))
                {
                    _logger.LogWarning($"Unable to resolve assembly '{referencePath}'");
                }
                else
                {
                    var metadataReference = _metadataReferenceCache.GetMetadataReference(referencePath);

                    if (unusedReferences.Remove(metadataReference))
                    {
                        continue;
                    }

                    _logger.LogDebug($"Adding reference '{referencePath}' to '{projectFileInfo.ProjectFilePath}'.");
                    _workspace.AddMetadataReference(project.Id, metadataReference);
                }
            }

            foreach (var reference in unusedReferences)
            {
                _workspace.RemoveMetadataReference(project.Id, reference);
            }
        }

        public ProjectFileInfo GetProject(string path)
        {
            ProjectFileInfo projectFileInfo;
            if (!_context.Projects.TryGetValue(path, out projectFileInfo))
            {
                return null;
            }

            return projectFileInfo;
        }

        Task<object> IProjectSystem.GetProjectModel(string path)
        {
            var document = _workspace.GetDocument(path);
            if (document == null)
                return Task.FromResult<object>(null);

            var project = GetProject(document.Project.FilePath);
            if (project == null)
                return Task.FromResult<object>(null);

            return Task.FromResult<object>(new MSBuildProject(project));
        }

        Task<object> IProjectSystem.GetInformationModel(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(new MsBuildWorkspaceInformation(_context, request?.ExcludeSourceFiles ?? false));
        }
    }
}
