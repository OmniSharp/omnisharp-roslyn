using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
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
    [Export(typeof(IProjectSystem)), Shared]
    public class MSBuildProjectSystem : IProjectSystem
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IOmnisharpEnvironment _environment;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IEventEmitter _eventEmitter;
        private readonly IMetadataFileReferenceCache _metadataReferenceCache;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly ILogger _logger;

        private readonly object _gate = new object();
        private readonly ProjectFileInfoCollection _projects;

        private MSBuildOptions _options;
        private string _solutionFileOrRootPath;

        private static readonly Guid[] _supportedProjectTypes = new[]
        {
            new Guid("fae04ec0-301f-11d3-bf4b-00c04f79efbc"), // CSharp
            new Guid("9A19103F-16F7-4668-BE54-9A1E7A4F7556")  // CSharp (New .NET Core csproj)
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
            IFileSystemWatcher fileSystemWatcher)
        {
            _workspace = workspace;
            _environment = environment;
            _loggerFactory = loggerFactory;
            _eventEmitter = eventEmitter;
            _fileSystemWatcher = fileSystemWatcher;
            _metadataReferenceCache = metadataReferenceCache;

            _projects = new ProjectFileInfoCollection();
            _logger = loggerFactory.CreateLogger("OmniSharp#MSBuild");
        }

        public void Initalize(IConfiguration configuration)
        {
            _options = new MSBuildOptions();
            ConfigurationBinder.Bind(configuration, _options);

            MSBuildEnvironment.Initialize(_logger);

            if (_options.WaitForDebugger)
            {
                Console.WriteLine($"Attach to process {Process.GetCurrentProcess().Id}");
                while (!Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }

            AddProjects();

            foreach (var projectFileInfo in _projects)
            {
                UpdateProject(projectFileInfo);

                // TODO: This needs some improvement. Currently, it tracks both deletions and changes
                // as "updates". We should properly remove projects that are deleted.
                _fileSystemWatcher.Watch(projectFileInfo.ProjectFilePath, OnProjectChanged);
            }
        }

        private void AddProjects()
        {
            if (!string.IsNullOrEmpty(_environment.SolutionFilePath))
            {
                // If a solution file path was provided, process that solution
                _solutionFileOrRootPath = _environment.SolutionFilePath;
                AddProjectsFromSolution(_environment.SolutionFilePath);
                return;
            }

            // Otherwise, assume that the path provided is a directory and
            // look for a solution there.
            var solutionFilePath = FindSolutionFilePath(_environment.Path, _logger);
            if (!string.IsNullOrEmpty(solutionFilePath))
            {
                _solutionFileOrRootPath = solutionFilePath;
                AddProjectsFromSolution(solutionFilePath);
                return;
            }

            // Finally, if there isn't a single solution immediately available,
            // Just process all of the projects beneath the root path.
            _solutionFileOrRootPath = _environment.Path;
            AddProjectsFromRootPath(_environment.Path);
        }

        private void AddProjectsFromSolution(string solutionFilePath)
        {
            _logger.LogInformation($"Detecting projects in '{solutionFilePath}'.");

            var solutionFile = ReadSolutionFile(solutionFilePath);
            var processedProjects = new HashSet<Guid>();

            foreach (var projectBlock in solutionFile.ProjectBlocks)
            {
                if (!_supportedProjectTypes.Contains(projectBlock.ProjectTypeGuid) &&
                    !UnityHelper.IsUnityProject(projectBlock.ProjectName, projectBlock.ProjectTypeGuid))
                {
                    _logger.LogWarning("Skipped unsupported project type '{0}'", projectBlock.ProjectPath);
                    continue;
                }

                // Have we seen this project GUID? If so, move on.
                if (processedProjects.Contains(projectBlock.ProjectGuid))
                {
                    continue;
                }

                // Solution files are assumed to contain relative paths to project files
                // with Windows-style slashes.
                var projectFilePath = projectBlock.ProjectPath.Replace('\\', Path.DirectorySeparatorChar);
                projectFilePath = Path.Combine(_environment.Path, projectFilePath);
                projectFilePath = Path.GetFullPath(projectFilePath);

                _logger.LogInformation($"Loading project from '{projectFilePath}'.");

                var projectFileInfo = AddProject(projectFilePath);
                if (projectFileInfo == null)
                {
                    continue;
                }

                processedProjects.Add(projectBlock.ProjectGuid);
            }
        }

        private void AddProjectsFromRootPath(string rootPath)
        {
            foreach (var projectFilePath in Directory.GetFiles(rootPath, "*.csproj", SearchOption.AllDirectories))
            {
                AddProject(projectFilePath);
            }
        }

        private ProjectFileInfo AddProject(string filePath)
        {
            if (_projects.ContainsKey(filePath))
            {
                _logger.LogWarning($"Can't add project, it already exists: {filePath}");
                return null;
            }

            var fileInfo = CreateProjectFileInfo(filePath);
            if (fileInfo == null)
            {
                return null;
            }

            _logger.LogInformation($"Add project: {fileInfo.ProjectFilePath}");

            _projects.Add(fileInfo);

            var compilationOptions = CreateCompilationOptions(fileInfo);

            var projectInfo = ProjectInfo.Create(
                id: ProjectId.CreateNewId(fileInfo.Name),
                version: VersionStamp.Create(),
                name: fileInfo.Name,
                assemblyName: fileInfo.AssemblyName,
                language: LanguageNames.CSharp,
                filePath: fileInfo.ProjectFilePath,
                compilationOptions: compilationOptions);

            _workspace.AddProject(projectInfo);

            fileInfo.SetProjectId(projectInfo.Id);

            return fileInfo;
        }

        private static CSharpCompilationOptions CreateCompilationOptions(ProjectFileInfo projectFileInfo)
        {
            var result = new CSharpCompilationOptions(projectFileInfo.OutputKind);

            result = result.WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default);

            if (projectFileInfo.AllowUnsafe)
            {
                result = result.WithAllowUnsafe(true);
            }

            var specificDiagnosticOptions = new Dictionary<string, ReportDiagnostic>(projectFileInfo.SuppressedDiagnosticIds.Count);

            // Ensure that specific warnings about assembly references are always suppressed.
            specificDiagnosticOptions.Add("CS1701", ReportDiagnostic.Suppress);
            specificDiagnosticOptions.Add("CS1702", ReportDiagnostic.Suppress);
            specificDiagnosticOptions.Add("CS1705", ReportDiagnostic.Suppress);

            if (projectFileInfo.SuppressedDiagnosticIds.Any())
            {
                foreach (var id in projectFileInfo.SuppressedDiagnosticIds)
                {
                    if (!specificDiagnosticOptions.ContainsKey(id))
                    {
                        specificDiagnosticOptions.Add(id, ReportDiagnostic.Suppress);
                    }
                }
            }

            result = result.WithSpecificDiagnosticOptions(specificDiagnosticOptions);

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

            return result.FilePath;
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
            var newProjectFileInfo = CreateProjectFileInfo(projectFilePath);

            // TODO: Should we remove the entry if the project is malformed?
            if (newProjectFileInfo != null)
            {
                lock (_gate)
                {
                    ProjectFileInfo oldProjectFileInfo;
                    if (_projects.TryGetValue(projectFilePath, out oldProjectFileInfo))
                    {
                        _projects[projectFilePath] = newProjectFileInfo;
                        newProjectFileInfo.SetProjectId(oldProjectFileInfo.ProjectId);
                        UpdateProject(newProjectFileInfo);
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

                // If the source file doesn't exist on disk, don't try to add it.
                if (!File.Exists(sourceFile))
                {
                    continue;
                }

                // If all is OK, add a new document.
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
            _logger.LogInformation($"Update project: {project.Name}");

            var existingProjectReferences = new HashSet<ProjectReference>(project.ProjectReferences);
            var addedProjectReferences = new HashSet<ProjectReference>();

            foreach (var projectReference in projectReferences)
            {
                ProjectFileInfo projectReferenceInfo;
                if (_projects.TryGetValue(projectReference, out projectReferenceInfo))
                {
                    var reference = new ProjectReference(projectReferenceInfo.ProjectId);

                    if (existingProjectReferences.Remove(reference))
                    {
                        // This reference already exists
                        continue;
                    }

                    if (!addedProjectReferences.Contains(reference))
                    {
                        _workspace.AddProjectReference(project.Id, reference);
                        addedProjectReferences.Add(reference);
                    }
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
            var addedReferences = new HashSet<MetadataReference>();

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

                    if (!addedReferences.Contains(metadataReference))
                    {
                        _logger.LogDebug($"Adding reference '{referencePath}' to '{project.Name}'.");
                        _workspace.AddMetadataReference(project.Id, metadataReference);
                        addedReferences.Add(metadataReference);
                    }
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
            if (!_projects.TryGetValue(path, out projectFileInfo))
            {
                return null;
            }

            return projectFileInfo;
        }

        Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(
                new MsBuildWorkspaceInformation(_solutionFileOrRootPath, _projects,
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
                new MSBuildProjectInformation(projectFileInfo));
        }
    }
}
