using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.DotNet.Cache;
using OmniSharp.DotNet.Extensions;
using OmniSharp.DotNet.Models;
using OmniSharp.DotNet.Tools;
using OmniSharp.Models;
using OmniSharp.Models.v1;
using OmniSharp.Services;

namespace OmniSharp.DotNet
{
    [Export(typeof(IProjectSystem)), Shared]
    public class DotNetProjectSystem : IProjectSystem
    {
        private readonly ILogger _logger;
        private readonly IEventEmitter _emitter;
        private readonly IFileSystemWatcher _watcher;
        private readonly IOmnisharpEnvironment _environment;
        private readonly IMetadataFileReferenceCache _metadataFileReferenceCache;
        private readonly string _compilationConfiguration = "Debug";

        private readonly PackagesRestoreTool _packageRestore;
        private readonly OmnisharpWorkspace _omnisharpWorkspace;
        private readonly ProjectStatesCache _projectStates;

        private WorkspaceContext _workspaceContext;

        [ImportingConstructor]
        public DotNetProjectSystem(IOmnisharpEnvironment environment,
                                   OmnisharpWorkspace omnisharpWorkspace,
                                   IMetadataFileReferenceCache metadataFileReferenceCache,
                                   ILoggerFactory loggerFactory,
                                   IFileSystemWatcher watcher,
                                   IEventEmitter emitter)
        {
            _environment = environment;
            _omnisharpWorkspace = omnisharpWorkspace;
            _logger = loggerFactory.CreateLogger<DotNetProjectSystem>();
            _emitter = emitter;
            _metadataFileReferenceCache = metadataFileReferenceCache;
            _watcher = watcher;

            _packageRestore = new PackagesRestoreTool(loggerFactory, _emitter);
            _projectStates = new ProjectStatesCache(loggerFactory);
        }

        public IEnumerable<string> Extensions { get; } = new string[] { ".cs" };

        public string Key { get { return "DotNet"; } }

        public string Language { get { return LanguageNames.CSharp; } }

        public Task<object> GetInformationModel(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(new DotNetWorkspaceInformation());
        }

        public Task<object> GetProjectModel(string path)
        {
            _logger.LogInformation($"GetProjectModel {path}");
            var document = _omnisharpWorkspace.GetDocument(path);
            if (document == null)
            {
                return Task.FromResult<object>(null);
            }

            var projectPath = document.Project.FilePath;
            _logger.LogInformation($"GetProjectModel {path}=>{projectPath}");
            //var projectInformation = _projectStates.Get(projectPath).FirstOrDefault()?.Information;
            //if (projectInformation == null)
            //{
            return Task.FromResult<object>(null);
            //}

            //return Task.FromResult<object>(new DotNetProjectInformation(projectPath, projectInformation));
        }

        public void Initalize(IConfiguration configuration)
        {
            _logger.LogInformation($"Initializing in {_environment.Path}");

            _workspaceContext = WorkspaceContext.CreateFrom(_environment.Path);
            if (_workspaceContext == null)
            {
                throw new NotImplementedException($"Failed to initialize {typeof(WorkspaceContext)} at {_environment.Path}.");
            }

            Update(allowRestore: true);
        }

        public void Update(bool allowRestore)
        {
            _logger.LogInformation("Update workspace context");
            _workspaceContext.Refresh();

            var projectPaths = _workspaceContext.GetAllProjects();

            _projectStates.RemoveExcept(projectPaths, id =>
            {
                _omnisharpWorkspace.RemoveProject(id);
                _logger.LogInformation($"Removing project {id.Id}.");
            });

            foreach (var projectPath in projectPaths)
            {
                UpdateProject(projectPath);
            }

            _logger.LogInformation("Resolving projects references");
            foreach (var state in _projectStates.Values)
            {
                _logger.LogInformation($"  Processing {state}");

                var lens = new ProjectContextLens(state.ProjectContext, _compilationConfiguration);
                UpdateFileReferences(state, lens.FileReferences);
                UpdateProjectReferences(state, lens.ProjectReferences);
                UpdateUnresolvedDependencies(state, allowRestore);
                UpdateCompilationOption(state);
                UpdateSourceFiles(state, lens.SourceFiles);
            }
        }

        private void UpdateProject(string projectDirectory)
        {
            _logger.LogInformation($"Update project {projectDirectory}");
            var contexts = _workspaceContext.GetProjectContexts(projectDirectory);

            if (!contexts.Any())
            {
                _logger.LogWarning($"Cannot create any {nameof(ProjectContext)} from project {projectDirectory}");
                return;
            }

            var projectFilePath = contexts.First().ProjectFile.ProjectFilePath;

            _emitter.Emit(
                EventTypes.ProjectChanged,
                new ProjectInformationResponse()
                {
                    // the key is hard coded in VSCode
                    {
                        "DnxProject",
                        new
                        {
                            Path = projectFilePath,
                            SourceFiles = Enumerable.Empty<string>()
                        }
                    }
                });

            _projectStates.Update(projectDirectory, contexts, AddProject, _omnisharpWorkspace.RemoveProject);

            _watcher.Watch(projectFilePath, file =>
            {
                _logger.LogInformation($"Watcher: {file} updated.");
                Update(true);
            });

            _watcher.Watch(Path.ChangeExtension(projectFilePath, "lock.json"), file =>
           {
               _logger.LogInformation($"Watcher: {file} updated.");
               Update(false);
           });
        }

        private void UpdateFileReferences(ProjectState state, IEnumerable<string> fileReferences)
        {
            var metadataReferences = new List<MetadataReference>();
            var fileReferencesToRemove = state.FileMetadataReferences.Keys.ToHashSet();

            foreach (var fileReference in fileReferences)
            {
                if (!File.Exists(fileReference))
                {
                    continue;
                }

                if (fileReferencesToRemove.Remove(fileReference))
                {
                    continue;
                }

                var metadataReference = _metadataFileReferenceCache.GetMetadataReference(fileReference);
                state.FileMetadataReferences[fileReference] = metadataReference;
                metadataReferences.Add(metadataReference);
                _logger.LogDebug($"    Add file reference {fileReference}");
            }

            foreach (var reference in metadataReferences)
            {
                _omnisharpWorkspace.AddMetadataReference(state.Id, reference);
            }

            foreach (var reference in fileReferencesToRemove)
            {
                var toRemove = state.FileMetadataReferences[reference];
                state.FileMetadataReferences.Remove(reference);
                _omnisharpWorkspace.RemoveMetadataReference(state.Id, toRemove);
                _logger.LogDebug($"    Remove file reference {reference}");
            }

            _logger.LogInformation($"    Added {metadataReferences.Count} and removed {fileReferencesToRemove.Count} file references");
        }

        private void UpdateProjectReferences(ProjectState state, IEnumerable<ProjectDescription> projectReferencesLatest)
        {
            var projectReferences = new List<ProjectReference>();
            var projectReferencesToRemove = state.ProjectReferences.Keys.ToHashSet();

            foreach (var description in projectReferencesLatest)
            {
                var projectPath = Path.GetDirectoryName(description.Path);
                if (projectReferencesToRemove.Remove(projectPath))
                {
                    continue;
                }

                var referencedProjectState = _projectStates.Find(projectPath, description.Framework);
                projectReferences.Add(new ProjectReference(referencedProjectState.Id));
                state.ProjectReferences[projectPath] = referencedProjectState.Id;

                _logger.LogDebug($"    Add project reference {description.Path}");
            }

            foreach (var reference in projectReferences)
            {
                _omnisharpWorkspace.AddProjectReference(state.Id, reference);
            }

            foreach (var reference in projectReferencesToRemove)
            {
                var toRemove = state.ProjectReferences[reference];
                state.ProjectReferences.Remove(reference);
                _omnisharpWorkspace.RemoveProjectReference(state.Id, new ProjectReference(toRemove));

                _logger.LogDebug($"    Remove project reference {reference}");
            }

            _logger.LogInformation($"    Added {projectReferences.Count} and removed {projectReferencesToRemove.Count} project references");
        }

        private void UpdateUnresolvedDependencies(ProjectState state, bool allowRestore)
        {
            var libraryManager = state.ProjectContext.LibraryManager;
            var allDiagnostics = libraryManager.GetAllDiagnostics();
            var unresolved = libraryManager.GetLibraries().Where(dep => !dep.Resolved);
            var needRestore = allDiagnostics.Any(diag => diag.ErrorCode == ErrorCodes.NU1006) || unresolved.Any();

            if (needRestore)
            {
                if (allowRestore)
                {
                    _packageRestore.Restore(state.ProjectContext.ProjectDirectory, onFailure: () =>
                    {
                        _emitter.Emit(EventTypes.UnresolvedDependencies, new UnresolvedDependenciesMessage()
                        {
                            FileName = state.ProjectContext.ProjectFile.ProjectFilePath,
                            UnresolvedDependencies = unresolved.Select(d => new PackageDependency { Name = d.Identity.Name, Version = d.Identity.Version.ToString() })
                        });
                    });
                }
                else
                {
                    _emitter.Emit(EventTypes.UnresolvedDependencies, new UnresolvedDependenciesMessage()
                    {
                        FileName = state.ProjectContext.ProjectFile.ProjectFilePath,
                        UnresolvedDependencies = unresolved.Select(d => new PackageDependency { Name = d.Identity.Name, Version = d.Identity.Version.ToString() })
                    });
                }
            }
        }

        private void UpdateCompilationOption(ProjectState state)
        {
            var context = state.ProjectContext;
            var project = context.ProjectFile;
            var option = project.GetCompilerOptions(context.TargetFramework, _compilationConfiguration);

            var outputKind = option.EmitEntryPoint.GetValueOrDefault() ? OutputKind.ConsoleApplication :
                                                                         OutputKind.DynamicallyLinkedLibrary;

            var generalDiagnosticOpt = (option.WarningsAsErrors ?? false) ? ReportDiagnostic.Error :
                                                                            ReportDiagnostic.Default;

            var optimize = (option.Optimize ?? false) ? OptimizationLevel.Release : OptimizationLevel.Debug;

            var csharpOptions = new CSharpCompilationOptions(outputKind)
                .WithAllowUnsafe(option.AllowUnsafe ?? false)
                .WithPlatform(ParsePlatfrom(option.Platform))
                .WithGeneralDiagnosticOption(generalDiagnosticOpt)
                .WithOptimizationLevel(optimize)
                .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic> {
                    { "CS1701", ReportDiagnostic.Suppress },
                    { "CS1702", ReportDiagnostic.Suppress },
                    { "CS1705", ReportDiagnostic.Suppress },
                })
                .WithConcurrentBuild(false); // TODO: actually just need to disable on mono

            var parseOptions = new CSharpParseOptions(languageVersion: ParseLanguageVersion(option.LanguageVersion),
                                                      preprocessorSymbols: option.Defines);

            _omnisharpWorkspace.SetCompilationOptions(state.Id, csharpOptions);
            _omnisharpWorkspace.SetParseOptions(state.Id, parseOptions);
        }

        private void UpdateSourceFiles(ProjectState state, IEnumerable<string> sourceFiles)
        {
            sourceFiles = sourceFiles.Where(filename => Path.GetExtension(filename) == ".cs");

            var existingFiles = new HashSet<string>(state.DocumentReferences.Keys);

            var added = 0;
            var removed = 0;

            foreach (var file in sourceFiles)
            {
                if (existingFiles.Remove(file))
                {
                    continue;
                }

                // TODO: performance optimize
                using (var stream = File.OpenRead(file))
                {
                    // TODO: other encoding option?
                    var sourceText = SourceText.From(stream, encoding: Encoding.UTF8);
                    var docId = DocumentId.CreateNewId(state.Id);
                    var version = VersionStamp.Create();

                    var loader = TextLoader.From(TextAndVersion.Create(sourceText, version));


                    var doc = DocumentInfo.Create(docId, file, filePath: file, loader: loader);
                    _omnisharpWorkspace.AddDocument(doc);
                    state.DocumentReferences[file] = doc.Id;

                    _logger.LogDebug($"    Added document {file}.");
                    added++;
                }
            }

            foreach (var file in existingFiles)
            {
                _omnisharpWorkspace.RemoveDocument(state.DocumentReferences[file]);
                state.DocumentReferences.Remove(file);
                _logger.LogDebug($"    Removed document {file}.");
                removed++;
            }

            _logger.LogInformation($"    Added {added} and removed {removed} documents.");
        }

        private void AddProject(ProjectId id, ProjectContext context)
        {
            var info = ProjectInfo.Create(
                id,
                VersionStamp.Create(),
                $"{context.ProjectFile.Name}+{context.TargetFramework.GetShortFolderName()}",
                context.ProjectFile.Name,
                LanguageNames.CSharp,
                context.ProjectFile.ProjectFilePath);

            _omnisharpWorkspace.AddProject(info);

            _logger.LogInformation($"Add project {context.ProjectFile.ProjectFilePath} => {id.Id}");
        }

        private static Platform ParsePlatfrom(string value)
        {
            Platform platform;
            if (!Enum.TryParse<Platform>(value, ignoreCase: true, result: out platform))
            {
                platform = Platform.AnyCpu;
            }

            return platform;
        }

        private static LanguageVersion ParseLanguageVersion(string value)
        {
            LanguageVersion languageVersion;
            if (!Enum.TryParse<LanguageVersion>(value, ignoreCase: true, result: out languageVersion))
            {
                languageVersion = LanguageVersion.CSharp6;
            }

            return languageVersion;
        }
    }
}
