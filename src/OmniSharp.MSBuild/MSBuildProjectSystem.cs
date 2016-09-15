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
        private readonly ILogger _logger;

        private MSBuildOptions _options;
        private string _solutionFilePath;

        private readonly object _gate = new object();
        public Dictionary<string, ProjectFileInfo> Projects { get; } = new Dictionary<string, ProjectFileInfo>(StringComparer.OrdinalIgnoreCase);

        private static readonly Guid[] _supportsProjectTypes = new[] {
            new Guid("fae04ec0-301f-11d3-bf4b-00c04f79efbc") // CSharp
        };

        public string Key { get; } = "MsBuild";
        public string Language { get; } = LanguageNames.CSharp;
        public IEnumerable<string> Extensions { get; } = new[] { ".cs" };

        public MSBuildProjectSystem() { }

        [ImportingConstructor]
        public MSBuildProjectSystem(
            OmnisharpWorkspace workspace,
            IOmnisharpEnvironment environment,
            ILoggerFactory loggerFactory,
            IEventEmitter eventEmitter,
            IMetadataFileReferenceCache metadataReferenceCache,
            IFileSystemWatcher fileSystemWatcher)
        {
            _workspace = workspace;
            _environment = environment;
            _loggerFactory = loggerFactory;
            _eventEmitter = eventEmitter;
            _fileSystemWatcher = fileSystemWatcher;
            _metadataReferenceCache = metadataReferenceCache;

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

            _solutionFilePath = solutionFilePath;

            _logger.LogInformation($"Detecting projects in '{solutionFilePath}'.");

            var solutionFile = ReadSolutionFile(solutionFilePath);

            var projectGuidSet = new HashSet<Guid>();

            foreach (var projectBlock in solutionFile.ProjectBlocks)
            {
                if (!_supportsProjectTypes.Contains(projectBlock.ProjectTypeGuid) &&
                    !UnityHelper.IsUnityProject(projectBlock))
                {
                    _logger.LogWarning("Skipped unsupported project type '{0}'", projectBlock.ProjectPath);
                    continue;
                }

                // Have we seen this project GUID? If so, move on.
                if (projectGuidSet.Contains(projectBlock.ProjectGuid))
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

                projectFileInfo.SetProjectId(projectInfo.Id);

                Projects[projectFileInfo.ProjectFilePath] = projectFileInfo;
                projectGuidSet.Add(projectBlock.ProjectGuid);

                _fileSystemWatcher.Watch(projectFilePath, OnProjectChanged);
            }

            foreach (var projectFileInfo in Projects.Values)
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
                projectFileInfo = ProjectFileInfo.Create(projectFilePath, _environment.Path, _loggerFactory.CreateLogger("OmniSharp#ProjectFileInfo"), _options, diagnostics);

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
                lock (_gate)
                {
                    ProjectFileInfo oldProjectFileInfo;
                    if (Projects.TryGetValue(projectFilePath, out oldProjectFileInfo))
                    {
                        Projects[projectFilePath] = newProjectInfo;
                        newProjectInfo.SetProjectId(oldProjectFileInfo.ProjectId);
                        UpdateProject(newProjectInfo);
                    }
                }
            }
        }

        private void UpdateProject(ProjectFileInfo projectFileInfo)
        {
            var project = _workspace.CurrentSolution.GetProject(projectFileInfo.ProjectId);
            if (project == null)
            {
                _logger.LogError($"Could not locate project in workspace: {projectFileInfo.ProjectFilePath}");
                return;
            }

            UpdateSourceFiles(project, projectFileInfo.SourceFiles);
            UpdateParseOptions(project, projectFileInfo.SpecifiedLanguageVersion, projectFileInfo.PreprocessorSymbolNames, projectFileInfo.GenerateXmlDocumentation);
            UpdateProjectReferences(project, projectFileInfo.ProjectReferences);
            UpdateReferences(project, projectFileInfo.References);
        }

        private void UpdateSourceFiles(Project project, IList<string> sourceFiles)
        {
            var currentDocuments = project.Documents.ToDictionary(d => d.FilePath, d => d.Id);

            // Add source files to the project.
            foreach (var sourceFile in sourceFiles)
            {
                // If a document for this source file already exists in the project, carry on.
                if (currentDocuments.Remove(sourceFile))
                {
                    continue;
                }

                // If not, add a new document.
                using (var stream = File.OpenRead(sourceFile))
                {
                    var sourceText = SourceText.From(stream, encoding: Encoding.UTF8);
                    var documentId = DocumentId.CreateNewId(project.Id);
                    var version = VersionStamp.Create();
                    var loader = TextLoader.From(TextAndVersion.Create(sourceText, version));
                    var documentInfo = DocumentInfo.Create(documentId, sourceFile, filePath: sourceFile, loader: loader);

                    _workspace.AddDocument(documentInfo);
                }
            }

            // Removing any remaining documents from the project.
            foreach (var currentDocument in currentDocuments)
            {
                _workspace.RemoveDocument(currentDocument.Value);
            }
        }

        private void UpdateParseOptions(Project project, LanguageVersion languageVersion, IEnumerable<string> preprocessorSymbolNames, bool generateXmlDocumentation)
        {
            var existingParseOptions = (CSharpParseOptions)project.ParseOptions;

            if (existingParseOptions.LanguageVersion == languageVersion &&
                Enumerable.SequenceEqual(existingParseOptions.PreprocessorSymbolNames, preprocessorSymbolNames) &&
                (existingParseOptions.DocumentationMode == DocumentationMode.Diagnose) == generateXmlDocumentation)
            {
                // No changes to make. Moving on.
                return;
            }

            var parseOptions = new CSharpParseOptions(languageVersion);

            if (preprocessorSymbolNames.Any())
            {
                parseOptions = parseOptions.WithPreprocessorSymbols(preprocessorSymbolNames);
            }

            if (generateXmlDocumentation)
            {
                parseOptions = parseOptions.WithDocumentationMode(DocumentationMode.Diagnose);
            }

            _workspace.SetParseOptions(project.Id, parseOptions);
        }

        private void UpdateProjectReferences(Project project, IList<string> projectReferences)
        {
            var existingProjectReferences = new HashSet<ProjectReference>(project.ProjectReferences);

            foreach (var projectReference in projectReferences)
            {
                ProjectFileInfo projectReferenceInfo;
                if (Projects.TryGetValue(projectReference, out projectReferenceInfo))
                {
                    var reference = new ProjectReference(projectReferenceInfo.ProjectId);

                    if (existingProjectReferences.Remove(reference))
                    {
                        // This reference already exists
                        continue;
                    }

                    _workspace.AddProjectReference(project.Id, reference);
                }
                else
                {
                    _logger.LogWarning($"Unable to resolve project reference '{projectReference}' for '{project.Name}'.");
                }
            }

            foreach (var existingProjectReference in existingProjectReferences)
            {
                _workspace.RemoveProjectReference(project.Id, existingProjectReference);
            }
        }

        private void UpdateReferences(Project project, IList<string> references)
        {
            var existingReferences = new HashSet<MetadataReference>(project.MetadataReferences);

            foreach (var referencePath in references)
            {
                if (!File.Exists(referencePath))
                {
                    _logger.LogWarning($"Unable to resolve assembly '{referencePath}'");
                }
                else
                {
                    var metadataReference = _metadataReferenceCache.GetMetadataReference(referencePath);

                    if (existingReferences.Remove(metadataReference))
                    {
                        continue;
                    }

                    _logger.LogDebug($"Adding reference '{referencePath}' to '{project.Name}'.");
                    _workspace.AddMetadataReference(project.Id, metadataReference);
                }
            }

            foreach (var existingReference in existingReferences)
            {
                _workspace.RemoveMetadataReference(project.Id, existingReference);
            }
        }

        private ProjectFileInfo GetProjectFileInfo(string path)
        {
            ProjectFileInfo projectFileInfo;
            if (!Projects.TryGetValue(path, out projectFileInfo))
            {
                return null;
            }

            return projectFileInfo;
        }

        Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(
                new MsBuildWorkspaceInformation(_solutionFilePath, Projects,
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
