using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using OmniSharp.MSBuild.ProjectFile;
using OmniSharp.Options;
using OmniSharp.Services;

namespace OmniSharp.MSBuild
{
    [Export(typeof(IProjectSystem))]
    public class MSBuildProjectSystem : IProjectSystem
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IOmnisharpEnvironment _environment;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IEventEmitter _eventEmitter;
        private readonly IMetadataFileReferenceCache _metadataReferenceCache;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly MSBuildContext _context;
        private readonly ILogger _logger;

        private MSBuildOptions _options;

        private static readonly Guid[] _supportsProjectTypes = new[] {
            new Guid("fae04ec0-301f-11d3-bf4b-00c04f79efbc") // CSharp
        };

        public string Key { get; } = "MsBuild";
        public string Language { get; } = LanguageNames.CSharp;
        public IEnumerable<string> Extensions { get; } = new[] { ".cs" };

        [ImportingConstructor]
        public MSBuildProjectSystem(
            OmnisharpWorkspace workspace,
            IOmnisharpEnvironment environment,
            ILoggerFactory loggerFactory,
            IEventEmitter eventEmitter,
            IMetadataFileReferenceCache metadataReferenceCache,
            IFileSystemWatcher fileSystemWatcher,
            MSBuildContext context)
        {
            _workspace = workspace;
            _environment = environment;
            _loggerFactory = loggerFactory;
            _eventEmitter = eventEmitter;
            _fileSystemWatcher = fileSystemWatcher;
            _metadataReferenceCache = metadataReferenceCache;
            _context = context;

            _logger = loggerFactory.CreateLogger("OmniSharp#MSBuild");
        }

        public void Initalize(IConfiguration configuration)
        {
            _options = new MSBuildOptions();
            ConfigurationBinder.Bind(configuration, _options);

            if (_options.WaitForDebugger)
            {
                Console.WriteLine($"Attach to process {System.Diagnostics.Process.GetCurrentProcess().Id}");
                while (!System.Diagnostics.Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }

            var solutionFilePath = _environment.SolutionFilePath;
            if (string.IsNullOrEmpty(solutionFilePath))
            {
                solutionFilePath = FindSolutionFilePath(_environment.Path, _logger);

                if (string.IsNullOrEmpty(solutionFilePath))
                {
                    return;
                }
            }

            _context.SolutionPath = solutionFilePath;

            _logger.LogInformation($"Detecting projects in '{solutionFilePath}'.");

            var solutionFile = ReadSolutionFile(solutionFilePath);

            foreach (var projectBlock in solutionFile.ProjectBlocks)
            {
                if (!_supportsProjectTypes.Contains(projectBlock.ProjectTypeGuid) &&
                    !UnityHelper.IsUnityProject(projectBlock))
                {
                    _logger.LogWarning("Skipped unsupported project type '{0}'", projectBlock.ProjectPath);
                    continue;
                }

                if (_context.ProjectGuidToProjectIdMap.ContainsKey(projectBlock.ProjectGuid))
                {
                    continue;
                }

                var projectFilePath = projectBlock.ProjectPath.Replace('\\', Path.DirectorySeparatorChar);
                projectFilePath = Path.Combine(_environment.Path, projectFilePath);
                projectFilePath = Path.GetFullPath(projectFilePath);

                _logger.LogInformation($"Loading project from '{projectFilePath}'.");

                var projectFileInfo = CreateProjectFileInfo(projectFilePath);
                if (projectFileInfo == null)
                {
                    continue;
                }

                var compilationOptions = CreateCompilationOptions(projectFileInfo);

                var projectInfo = ProjectInfo.Create(
                    id: ProjectId.CreateNewId(projectFileInfo.Name),
                    version: VersionStamp.Create(),
                    name: projectFileInfo.Name,
                    assemblyName: projectFileInfo.AssemblyName,
                    language: LanguageNames.CSharp,
                    filePath: projectFileInfo.ProjectFilePath,
                    compilationOptions: compilationOptions);

                _workspace.AddProject(projectInfo);

                projectFileInfo.ProjectId = projectInfo.Id;

                _context.Projects[projectFileInfo.ProjectFilePath] = projectFileInfo;
                _context.ProjectGuidToProjectIdMap[projectBlock.ProjectGuid] = projectInfo.Id;

                _fileSystemWatcher.Watch(projectFilePath, OnProjectChanged);
            }

            foreach (var projectFileInfo in _context.Projects.Values)
            {
                UpdateProject(projectFileInfo);
            }
        }

        private static CSharpCompilationOptions CreateCompilationOptions(ProjectFileInfo projectFileInfo)
        {
            var result = new CSharpCompilationOptions(projectFileInfo.OutputKind);

            result = result.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default);

            if (projectFileInfo.AllowUnsafe)
            {
                result = result.WithAllowUnsafe(true);
            }

            if (projectFileInfo.SignAssembly && !string.IsNullOrEmpty(projectFileInfo.AssemblyOriginatorKeyFile))
            {
                var keyFile = Path.Combine(projectFileInfo.ProjectDirectory, projectFileInfo.AssemblyOriginatorKeyFile);
                result = result.WithStrongNameProvider(new DesktopStrongNameProvider())
                               .WithCryptoKeyFile(keyFile);
            }

            if (projectFileInfo.GenerateXmlDocumentation)
            {
                result = result.WithXmlReferenceResolver(XmlFileResolver.Default);
            }

            return result;
        }

        private static SolutionFile ReadSolutionFile(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var reader = new StreamReader(stream))
            {
                return SolutionFile.Parse(reader);
            }
        }

        private static string FindSolutionFilePath(string rootPath, ILogger logger)
        {
            var solutionsFilePaths = Directory.GetFiles(rootPath, "*.sln");
            var result = SolutionSelector.Pick(solutionsFilePaths, rootPath);

            if (result.Message != null)
            {
                logger.LogInformation(result.Message);
            }

            return result.Solution;
        }

        private ProjectFileInfo CreateProjectFileInfo(string projectFilePath)
        {
            ProjectFileInfo projectFileInfo = null;
            var diagnostics = new List<MSBuildDiagnosticsMessage>();

            try
            {
                projectFileInfo = ProjectFileInfo.Create(_options, _loggerFactory.CreateLogger("OmniSharp#ProjectFileInfo"), _environment.Path, projectFilePath, diagnostics);

                if (projectFileInfo == null)
                {
                    _logger.LogWarning($"Failed to process project file '{projectFilePath}'.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to process project file '{projectFilePath}'.", ex);
                _eventEmitter.Emit(EventTypes.Error, new ErrorMessage()
                {
                    FileName = projectFilePath,
                    Text = ex.ToString()
                });
            }

            _eventEmitter.Emit(EventTypes.MsBuildProjectDiagnostics, new MSBuildProjectDiagnostics()
            {
                FileName = projectFilePath,
                Warnings = diagnostics.Where(d => d.LogLevel == "Warning"),
                Errors = diagnostics.Where(d => d.LogLevel == "Error"),
            });

            return projectFileInfo;
        }

        private void OnProjectChanged(string projectFilePath)
        {
            var newProjectInfo = CreateProjectFileInfo(projectFilePath);

            // Should we remove the entry if the project is malformed?
            if (newProjectInfo != null)
            {
                lock (_context)
                {
                    ProjectFileInfo oldProjectFileInfo;
                    if (_context.Projects.TryGetValue(projectFilePath, out oldProjectFileInfo))
                    {
                        _context.Projects[projectFilePath] = newProjectInfo;
                        newProjectInfo.ProjectId = oldProjectFileInfo.ProjectId;
                        UpdateProject(newProjectInfo);
                    }
                }
            }
        }

        private void UpdateProject(ProjectFileInfo projectFileInfo)
        {
            var project = _workspace.CurrentSolution.GetProject(projectFileInfo.ProjectId);

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
                    var id = DocumentId.CreateNewId(projectFileInfo.ProjectId);
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
                if (projectFileInfo.GenerateXmlDocumentation)
                {
                    parseOptions = parseOptions.WithDocumentationMode(DocumentationMode.Diagnose);
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
                    var reference = new ProjectReference(projectReferenceInfo.ProjectId);

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

        private ProjectFileInfo GetProjectFileInfo(string path)
        {
            ProjectFileInfo projectFileInfo;
            if (!_context.Projects.TryGetValue(path, out projectFileInfo))
            {
                return null;
            }

            return projectFileInfo;
        }

        Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(
                new MsBuildWorkspaceInformation(_context,
                    excludeSourceFiles: request?.ExcludeSourceFiles ?? false));
        }

        Task<object> IProjectSystem.GetProjectModelAsync(string filePath)
        {
            var document = _workspace.GetDocument(filePath);

            var projectFilePath = document != null
                ? document.Project.FilePath
                : filePath;

            var projectFileInfo = GetProjectFileInfo(projectFilePath);
            if (projectFileInfo == null)
            {
                _logger.LogDebug($"Could not locate project for '{projectFilePath}'");
                return Task.FromResult<object>(null);
            }

            return Task.FromResult<object>(
                new MSBuildProject(projectFileInfo));
        }
    }
}
