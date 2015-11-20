using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ProjectModel;
using Microsoft.Extensions.ProjectModel.ProjectSystem;
using OmniSharp.DotNet.Containers;
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
        private readonly ProjectCollection _cache;

        private ProjectSystem _projectJsonWorkspace;

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

            _cache = new ProjectCollection();
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
            var document = _omnisharpWorkspace.GetDocument(path);
            if (document == null)
            {
                return Task.FromResult<object>(null);
            }

            var projectPath = document.Project.FilePath;
            var projectInformation = _cache.Get(projectPath).FirstOrDefault()?.Information;
            if (projectInformation == null)
            {
                return Task.FromResult<object>(null);
            }

            return Task.FromResult<object>(new DotNetProjectInformation(projectPath, projectInformation));
        }

        public void Initalize(IConfiguration configuration)
        {
            _logger.LogInformation($"Initializing in {_environment.Path}");

            if (File.Exists(Path.Combine(_environment.Path, GlobalSettings.FileName)))
            {
                _logger.LogInformation($"Initialize project system at {_environment.Path}.");
                _projectJsonWorkspace = ProjectSystem.Create(_environment.Path);

                Update();
            }
            else
            {
                // single project solution
                throw new NotImplementedException($"{typeof(ProjectSystem)} requires a global json.");
            }
        }

        private void Update()
        {
            UpdateAsync().Wait();
        }

        private async Task UpdateAsync(CancellationToken cancellation = default(CancellationToken))
        {
            _logger.LogInformation("Update projects ...");
            var projectPaths = _projectJsonWorkspace.GetProjectPaths();
            RemoveProjectsAsync(projectPaths);
            await Task.WhenAll(projectPaths.Select(path => InitializeProjectAsync(path, cancellation)));

            _logger.LogInformation("Resolving project references ...");
            var allContexts = _cache.GetValues();
            foreach (var context in allContexts)
            {
                _logger.LogInformation($"Processing {context.Path}/{context.TargetFramework}");

                await UpdateMetadataReferencesAsync(context);
                await UpdateUnresolvedDependenciesAsync(context);
                await UpdateCompilationOptionAsync(context);
                await UpdateSourceFilesAsync(context);
            }
        }

        private void RemoveProjectsAsync(ISet<string> latestProjects)
        {
            foreach (var projectPath in _cache.GetKeys())
            {
                if (!latestProjects.Contains(projectPath))
                {
                    foreach (var context in _cache.Remove(projectPath))
                    {
                        _logger.LogInformation($"Removing project {context.Path}/{context.TargetFramework}");
                        _omnisharpWorkspace.RemoveProject(context.Id);
                    }
                }
            }
        }

        private async Task InitializeProjectAsync(string projectPath, CancellationToken cancellation = default(CancellationToken))
        {
            var projectInformation = await _projectJsonWorkspace.GetProjectInformationAsync(projectPath);
            var config = projectInformation.ChooseCompilationConfig(_compilationConfiguration);

            _emitter.Emit(
                EventTypes.ProjectChanged,
                new ProjectInformationResponse()
                {
                    // the key is hard coded in VSCode
                    { "DnxProject", new {Path = projectPath, SourceFiles = Enumerable.Empty<string>() } }
                });

            foreach (var targetFramework in projectInformation.Frameworks)
            {
                var projectWithFramework = _cache.Get(projectPath)
                                                 .FirstOrDefault(pwf => pwf.TargetFramework == targetFramework);
                if (projectWithFramework != null)
                {
                    continue;
                }

                var projectId = _cache.Add(projectPath, targetFramework, projectInformation);
                var info = ProjectInfo.Create(
                    projectId,
                    VersionStamp.Create(),
                    $"{projectInformation.Name}+{targetFramework.GetShortFolderName()}",
                    projectInformation.Name,
                    LanguageNames.CSharp,
                    projectPath);

                _omnisharpWorkspace.AddProject(info);
            }

            _watcher.Watch(projectPath, _ => Update());
            _watcher.Watch(Path.ChangeExtension(projectPath, "lock.json"), _ => Update());
        }

        private async Task UpdateMetadataReferencesAsync(ProjectWithFramework context)
        {
            var config = GetCompilationConfiguration(context);
            var metadataReferences = new List<MetadataReference>();
            var projectReferences = new List<ProjectReference>();

            var fileReferencesToRemove = context.FileMetadataReferences.Keys.ToHashSet();
            var fileReferencesLatest = await _projectJsonWorkspace.GetFileReferencesAsync(context.Path,
                                                                                          context.TargetFramework,
                                                                                          config);

            foreach (var fileReference in fileReferencesLatest)
            {
                if (fileReferencesToRemove.Remove(fileReference))
                {
                    continue;
                }

                var metadataReference = _metadataFileReferenceCache.GetMetadataReference(fileReference);
                context.FileMetadataReferences[fileReference] = metadataReference;
                metadataReferences.Add(metadataReference);
            }

            var projectReferencesToRemove = context.ProjectReferences.Keys.ToHashSet();
            var projectReferencesLatest = await _projectJsonWorkspace.GetProjectReferencesAsync(context.Path,
                                                                                               context.TargetFramework,
                                                                                               config);

            foreach (var projectRef in projectReferencesLatest)
            {
                if (projectReferencesToRemove.Remove(projectRef.Path))
                {
                    continue;
                }

                var referencedProject = _cache.Get(projectRef.Path)
                                              .Single(pwf => pwf.TargetFramework == context.TargetFramework);

                projectReferences.Add(new ProjectReference(referencedProject.Id));
            }

            foreach (var reference in metadataReferences)
            {
                _omnisharpWorkspace.AddMetadataReference(context.Id, reference);
            }

            foreach (var reference in fileReferencesToRemove)
            {
                var toRemove = context.FileMetadataReferences[reference];
                context.FileMetadataReferences.Remove(reference);
                _omnisharpWorkspace.RemoveMetadataReference(context.Id, toRemove);
            }

            foreach (var reference in projectReferences)
            {
                _omnisharpWorkspace.AddProjectReference(context.Id, reference);
            }

            foreach (var reference in projectReferencesToRemove)
            {
                var toRemove = context.ProjectReferences[reference];
                context.ProjectReferences.Remove(reference);
                _omnisharpWorkspace.RemoveProjectReference(context.Id, new ProjectReference(toRemove));
            }

            _logger.LogInformation($"Added {metadataReferences.Count} file references and {projectReferences.Count} project references.");
        }

        private async Task UpdateUnresolvedDependenciesAsync(ProjectWithFramework context)
        {
            var unresolved = await _projectJsonWorkspace.GetDependenciesAsync(
                context.Path,
                context.TargetFramework,
                GetCompilationConfiguration(context));

            unresolved = unresolved.Where(dep => !dep.Resolved);
            if (unresolved.Any())
            {
                _logger.LogInformation($"Project {context.Path} has these unresolved references: {string.Join(",", unresolved.Select(d => d.Name))} under {context.TargetFramework}.");
                _emitter.Emit(EventTypes.UnresolvedDependencies, new UnresolvedDependenciesMessage()
                {
                    FileName = context.Path,
                    UnresolvedDependencies = unresolved.Select(d => new PackageDependency { Name = d.Name, Version = d.Version })
                });

                _packageRestore.Restore(context.Path);
            }
        }

        private async Task UpdateCompilationOptionAsync(ProjectWithFramework context)
        {
            var option = await _projectJsonWorkspace.GetCompilerOptionAsync(
                context.Path,
                context.TargetFramework,
                GetCompilationConfiguration(context));

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

            _omnisharpWorkspace.SetCompilationOptions(context.Id, csharpOptions);
            _omnisharpWorkspace.SetParseOptions(context.Id, parseOptions);
        }

        private async Task UpdateSourceFilesAsync(ProjectWithFramework context)
        {
            var sources = await _projectJsonWorkspace.GetSourcesAsync(
                context.Path,
                context.TargetFramework,
                GetCompilationConfiguration(context));

            sources = sources.Where(filename => Path.GetExtension(filename) == ".cs");

            foreach (var file in sources)
            {
                // TODO: performance optimize
                using (var stream = File.OpenRead(file))
                {
                    // TODO: other encoding option?
                    var sourceText = SourceText.From(stream, encoding: Encoding.UTF8);
                    var docId = DocumentId.CreateNewId(context.Id);
                    var version = VersionStamp.Create();

                    var loader = TextLoader.From(TextAndVersion.Create(sourceText, version));
                    _omnisharpWorkspace.AddDocument(DocumentInfo.Create(docId, file, filePath: file, loader: loader));
                }
            }
        }

        private string GetCompilationConfiguration(ProjectWithFramework context)
        {
            return context.Information.ChooseCompilationConfig(_compilationConfiguration);
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
