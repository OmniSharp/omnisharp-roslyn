using System;
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
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Services;

namespace OmniSharp.Cake
{
    [Export(typeof(IProjectSystem)), Shared]
    public class CakeProjectSystem : IProjectSystem
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly IOmniSharpEnvironment _environment;
        private readonly IAssemblyLoader _assemblyLoader;
        private readonly ICakeScriptService _scriptService;
        private readonly ILogger<CakeProjectSystem> _logger;
        private readonly Dictionary<string, ProjectInfo> _projects;
        private readonly Lazy<CSharpCompilationOptions> _compilationOptions;

        public string Key => "Cake";
        public string Language => Constants.LanguageNames.Cake;
        public IEnumerable<string> Extensions => new[] { ".cake" };

        [ImportingConstructor]
        public CakeProjectSystem(
            OmniSharpWorkspace workspace,
            IOmniSharpEnvironment environment,
            IAssemblyLoader assemblyLoader,
            ICakeScriptService scriptService,
            ILoggerFactory loggerFactory)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _assemblyLoader = assemblyLoader ?? throw new ArgumentNullException(nameof(assemblyLoader));
            _scriptService = scriptService ?? throw new ArgumentNullException(nameof(scriptService));
            _logger = loggerFactory?.CreateLogger<CakeProjectSystem>() ?? throw new ArgumentNullException(nameof(loggerFactory));

            _projects = new Dictionary<string, ProjectInfo>();
            _compilationOptions = new Lazy<CSharpCompilationOptions>(CreateCompilationOptions);
        }

        public void Initalize(IConfiguration configuration)
        {
            _logger.LogInformation($"Detecting Cake files in '{_environment.TargetDirectory}'.");

            // Nothing to do if there are no Cake files
            var allCakeFiles = Directory.GetFiles(_environment.TargetDirectory, "*.cake", SearchOption.AllDirectories);
            if (allCakeFiles.Length == 0)
            {
                _logger.LogInformation("Could not find any Cake files");
                return;
            }

            _logger.LogInformation($"Found {allCakeFiles.Length} Cake files.");

            // Check that script service is connected
            if (!_scriptService.IsConnected)
            {
                _logger.LogWarning("Cake script service not connected. Aborting.");
                return;
            }

            foreach (var cakePath in allCakeFiles)
            {
                try
                {
                    var cakeScript = _scriptService.Generate(new FileChange
                    {
                        FileName = cakePath,
                        FromDisk = true
                    });
                    var project = GetProject(cakeScript, cakePath);

                    // add Cake project to workspace
                    _workspace.AddProject(project);
                    var documentId = DocumentId.CreateNewId(project.Id);
                    var loader = new CakeTextLoader(cakePath, _scriptService);
                    var documentInfo = DocumentInfo.Create(
                        documentId,
                        cakePath,
                        filePath: cakePath,
                        loader: loader,
                        sourceCodeKind: SourceCodeKind.Script);

                    _workspace.AddDocument(documentInfo);
                    _projects[cakePath] = project;
                    _logger.LogInformation($"Added Cake project '{cakePath}' to the workspace.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{cakePath} will be ignored due to an following error");
                }
            }

            // Hook up Cake script events
            _scriptService.ReferencesChanged += ScriptReferencesChanged;
            _scriptService.UsingsChanged += ScriptUsingsChanged;
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

                var metadataReferences = e.References.Select(reference => MetadataReference.CreateFromFile(reference, documentation: GetDocumentationProvider(reference)));
                var fileReferencesToRemove = project.MetadataReferences;

                foreach (var reference in metadataReferences)
                {
                    _workspace.AddMetadataReference(project.Id, reference);
                }

                foreach (var reference in fileReferencesToRemove)
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

            var assembly = _assemblyLoader.LoadFrom(cakeScript.Host.AssemblyPath);
            var hostObjectType = Type.GetType(cakeScript.Host.TypeName, a => assembly, null, true);

            return ProjectInfo.Create(
                id: ProjectId.CreateNewId(Guid.NewGuid().ToString()),
                version: VersionStamp.Create(),
                name: name,
                filePath: filePath,
                assemblyName: $"{name}.dll",
                language: LanguageNames.CSharp,
                compilationOptions: cakeScript.Usings == null ? _compilationOptions.Value : _compilationOptions.Value.WithUsings(cakeScript.Usings),
                parseOptions: new CSharpParseOptions(LanguageVersion.Default, DocumentationMode.Parse, SourceCodeKind.Script),
                metadataReferences: cakeScript.References.Select(reference => MetadataReference.CreateFromFile(reference, documentation: GetDocumentationProvider(reference))),
                // TODO: projectReferences?
                isSubmission: true,
                hostObjectType: hostObjectType);
        }

        private static DocumentationProvider GetDocumentationProvider(string assemblyPath)
        {
            var assemblyDocumentationPath = Path.ChangeExtension(assemblyPath, ".xml");
            return File.Exists(assemblyDocumentationPath)
                ? XmlDocumentationProvider.CreateFromFile(assemblyDocumentationPath)
                : DocumentationProvider.Default;
        }

        private static CSharpCompilationOptions CreateCompilationOptions()
        {
            var compilationOptions = new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    allowUnsafe: true,
                    metadataReferenceResolver: new CachingScriptMetadataResolver(),
                    sourceReferenceResolver: ScriptSourceResolver.Default,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default).
                WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
                {
                    // ensure that specific warnings about assembly references are always suppressed
                    // https://github.com/dotnet/roslyn/issues/5501
                    { "CS1701", ReportDiagnostic.Suppress },
                    { "CS1702", ReportDiagnostic.Suppress },
                    { "CS1705", ReportDiagnostic.Suppress }
                });

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
