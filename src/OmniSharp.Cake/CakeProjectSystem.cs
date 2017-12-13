using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cake.Scripting.Abstractions.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Configuration;
using OmniSharp.Cake.Services;
using OmniSharp.FileWatching;
using OmniSharp.Helpers;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Roslyn.Utilities;
using OmniSharp.Services;

namespace OmniSharp.Cake
{
    [Export(typeof(IProjectSystem)), Shared]
    public class CakeProjectSystem : IProjectSystem
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly MetadataFileReferenceCache _metadataReferenceCache;
        private readonly IOmniSharpEnvironment _environment;
        private readonly IAssemblyLoader _assemblyLoader;
        private readonly ICakeScriptService _scriptService;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly ILogger<CakeProjectSystem> _logger;
        private readonly ConcurrentDictionary<string, ProjectInfo> _projects;
        private readonly Lazy<CSharpCompilationOptions> _compilationOptions;

        private CakeOptions _options;

        public string Key => "Cake";
        public string Language => Constants.LanguageNames.Cake;
        public IEnumerable<string> Extensions => new[] { ".cake" };

        [ImportingConstructor]
        public CakeProjectSystem(
            OmniSharpWorkspace workspace,
            MetadataFileReferenceCache metadataReferenceCache,
            IOmniSharpEnvironment environment,
            IAssemblyLoader assemblyLoader,
            ICakeScriptService scriptService,
            IFileSystemWatcher fileSystemWatcher,
            ILoggerFactory loggerFactory)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _metadataReferenceCache = metadataReferenceCache ?? throw new ArgumentNullException(nameof(metadataReferenceCache));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _assemblyLoader = assemblyLoader ?? throw new ArgumentNullException(nameof(assemblyLoader));
            _scriptService = scriptService ?? throw new ArgumentNullException(nameof(scriptService));
            _fileSystemWatcher = fileSystemWatcher ?? throw new ArgumentNullException(nameof(fileSystemWatcher));
            _logger = loggerFactory?.CreateLogger<CakeProjectSystem>() ?? throw new ArgumentNullException(nameof(loggerFactory));

            _projects = new ConcurrentDictionary<string, ProjectInfo>();
            _compilationOptions = new Lazy<CSharpCompilationOptions>(CreateCompilationOptions);
        }

        public void Initalize(IConfiguration configuration)
        {
            _options = new CakeOptions();
            configuration.Bind(_options);

            _logger.LogInformation($"Detecting Cake files in '{_environment.TargetDirectory}'.");

            // Nothing to do if there are no Cake files
            var allCakeFiles = Directory.GetFiles(_environment.TargetDirectory, "*.cake", SearchOption.AllDirectories);
            if (allCakeFiles.Length == 0)
            {
                _logger.LogInformation("Could not find any Cake files");
                return;
            }

            _logger.LogInformation($"Found {allCakeFiles.Length} Cake files.");

            // Try intialize Cake scripting service
            if (!_scriptService.Initialize(_options))
            {
                _logger.LogWarning("Could not initialize Cake script service. Aborting.");
                return;
            }

            foreach (var cakeFilePath in allCakeFiles)
            {
                AddCakeFile(cakeFilePath);
            }

            // Hook up Cake script events
            _scriptService.ReferencesChanged += ScriptReferencesChanged;
            _scriptService.UsingsChanged += ScriptUsingsChanged;

            // Watch .cake files
            _fileSystemWatcher.Watch(".cake", OnCakeFileChanged);
        }

        private void AddCakeFile(string cakeFilePath)
        {
            try
            {
                var cakeScript = _scriptService.Generate(new FileChange
                {
                    FileName = cakeFilePath,
                    FromDisk = true
                });

                var project = GetProject(cakeScript, cakeFilePath);

                // add Cake project to workspace
                _workspace.AddProject(project);
                var documentId = DocumentId.CreateNewId(project.Id);
                var loader = new CakeTextLoader(cakeFilePath, _scriptService);
                var documentInfo = DocumentInfo.Create(
                    documentId,
                    cakeFilePath,
                    filePath: cakeFilePath,
                    loader: loader,
                    sourceCodeKind: SourceCodeKind.Script);

                _workspace.AddDocument(documentInfo);
                _projects[cakeFilePath] = project;
                _logger.LogInformation($"Added Cake project '{cakeFilePath}' to the workspace.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{cakeFilePath} will be ignored due to an following error");
            }
        }

        private void RemoveCakeFile(string cakeFilePath)
        {
            if (_projects.TryRemove(cakeFilePath, out var projectInfo))
            {
                _workspace.RemoveProject(projectInfo.Id);
                _logger.LogInformation($"Removed Cake project '{cakeFilePath}' from the workspace.");
            }
        }

        private void OnCakeFileChanged(string filePath, FileChangeType changeType)
        {
            if (changeType == FileChangeType.Unspecified && !File.Exists(filePath) || changeType == FileChangeType.Delete)
            {
                RemoveCakeFile(filePath);
            }

            if (changeType == FileChangeType.Unspecified && File.Exists(filePath) || changeType == FileChangeType.Create)
            {
                AddCakeFile(filePath);
            }
        }

        private void ScriptUsingsChanged(object sender, UsingsChangedEventArgs e)
        {
            var solution = _workspace.CurrentSolution;

            var documentIds = solution.GetDocumentIdsWithFilePath(e.ScriptPath);
            if (documentIds.IsEmpty)
            {
                return;
            }

            var compilationOptions = e.Usings == null
                ? _compilationOptions.Value
                : _compilationOptions.Value.WithUsings(e.Usings);

            foreach (var documentId in documentIds)
            {
                var document = solution.GetDocument(documentId);
                var project = document.Project;

                _workspace.SetCompilationOptions(project.Id, compilationOptions);
            }
        }

        private void ScriptReferencesChanged(object sender, ReferencesChangedEventArgs e)
        {
            var solution = _workspace.CurrentSolution;

            var documentIds = solution.GetDocumentIdsWithFilePath(e.ScriptPath);
            if (documentIds.IsEmpty)
            {
                return;
            }

            foreach (var documentId in documentIds)
            {
                var document = solution.GetDocument(documentId);
                var project = document.Project;

                var metadataReferences = GetMetadataReferences(e.References);
                var referencesToRemove = new HashSet<MetadataReference>(project.MetadataReferences, MetadataReferenceEqualityComparer.Instance);
                var referencesToAdd = new HashSet<MetadataReference>(MetadataReferenceEqualityComparer.Instance);

                foreach (var reference in metadataReferences)
                {
                    if (referencesToRemove.Remove(reference))
                    {
                        continue;
                    }

                    if (referencesToAdd.Contains(reference))
                    {
                        continue;
                    }

                    _workspace.AddMetadataReference(project.Id, reference);
                    referencesToAdd.Add(reference);
                }

                foreach (var reference in referencesToRemove)
                {
                    _workspace.RemoveMetadataReference(project.Id, reference);
                }
            }
        }

        public Task<object> GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            var scriptContextModels = new List<CakeContextModel>();
            foreach (var project in _projects)
            {
                scriptContextModels.Add(new CakeContextModel(project.Key));
            }
            return Task.FromResult<object>(new CakeContextModelCollection(scriptContextModels));
        }

        public Task<object> GetProjectModelAsync(string filePath)
        {
            // only react to .cake file paths
            if (!filePath.EndsWith(".cake", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<object>(null);
            }

            var document = _workspace.GetDocument(filePath);
            var projectFilePath = document != null
                ? document.Project.FilePath
                : filePath;

            var projectInfo = GetProjectFileInfo(projectFilePath);
            if (projectInfo == null)
            {
                _logger.LogDebug($"Could not locate project for '{projectFilePath}'");
                return Task.FromResult<object>(null);
            }

            return Task.FromResult<object>(new CakeContextModel(filePath));
        }

        private ProjectInfo GetProjectFileInfo(string path)
        {
            return !_projects.TryGetValue(path, out ProjectInfo projectFileInfo) ? null : projectFileInfo;
        }

        private ProjectInfo GetProject(CakeScript cakeScript, string filePath)
        {
            var name = Path.GetFileName(filePath);

            if (!File.Exists(cakeScript.Host.AssemblyPath))
            {
                throw new FileNotFoundException($"Cake is not installed. Path {cakeScript.Host.AssemblyPath} does not exist.");
            }
            var hostObjectType = Type.GetType(cakeScript.Host.TypeName, a => _assemblyLoader.LoadFrom(cakeScript.Host.AssemblyPath, dontLockAssemblyOnDisk: true), null, false);
            if (hostObjectType == null)
            {
                throw new InvalidOperationException($"Could not get host object type: {cakeScript.Host.TypeName}.");
            }

            return ProjectInfo.Create(
                id: ProjectId.CreateNewId(Guid.NewGuid().ToString()),
                version: VersionStamp.Create(),
                name: name,
                filePath: filePath,
                assemblyName: $"{name}.dll",
                language: LanguageNames.CSharp,
                compilationOptions: cakeScript.Usings == null ? _compilationOptions.Value : _compilationOptions.Value.WithUsings(cakeScript.Usings),
                parseOptions: new CSharpParseOptions(LanguageVersion.Default, DocumentationMode.Parse, SourceCodeKind.Script),
                metadataReferences: GetMetadataReferences(cakeScript.References),
                // TODO: projectReferences?
                isSubmission: true,
                hostObjectType: hostObjectType);
        }

        private IEnumerable<MetadataReference> GetMetadataReferences(IEnumerable<string> references)
        {
            foreach (var reference in references)
            {
                if (!File.Exists(reference))
                {
                    _logger.LogWarning($"Unable to create MetadataReference. File {reference} does not exist.");
                    continue;
                }

                yield return _metadataReferenceCache.GetMetadataReference(reference);
            }
        }

        private static CSharpCompilationOptions CreateCompilationOptions()
        {
            var compilationOptions = new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    allowUnsafe: true,
                    metadataReferenceResolver: new CachingScriptMetadataResolver(),
                    sourceReferenceResolver: ScriptSourceResolver.Default,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default).
                    WithSpecificDiagnosticOptions(CompilationOptionsHelper.GetDefaultSuppressedDiagnosticOptions());

            var topLevelBinderFlagsProperty = typeof(CSharpCompilationOptions).GetProperty("TopLevelBinderFlags", BindingFlags.Instance | BindingFlags.NonPublic);
            var binderFlagsType = typeof(CSharpCompilationOptions).GetTypeInfo().Assembly.GetType("Microsoft.CodeAnalysis.CSharp.BinderFlags");

            var ignoreCorLibraryDuplicatedTypesMember = binderFlagsType?.GetField("IgnoreCorLibraryDuplicatedTypes", BindingFlags.Static | BindingFlags.Public);
            var ignoreCorLibraryDuplicatedTypesValue = ignoreCorLibraryDuplicatedTypesMember?.GetValue(null);
            if (ignoreCorLibraryDuplicatedTypesValue != null)
            {
                topLevelBinderFlagsProperty?.SetValue(compilationOptions, ignoreCorLibraryDuplicatedTypesValue);
            }

            return compilationOptions;
        }
    }
}
