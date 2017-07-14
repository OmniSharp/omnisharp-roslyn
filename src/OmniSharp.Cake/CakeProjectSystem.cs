using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cake.Scripting.Abstractions;
using Cake.Scripting.Abstractions.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.Cake.Configuration;
using OmniSharp.Cake.Polyfill;
using OmniSharp.Cake.Tools;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Services;

namespace OmniSharp.Cake
{
    [Export(typeof(IProjectSystem)), Shared]
    public class CakeProjectSystem : IProjectSystem
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly IOmniSharpEnvironment _environment;
        private readonly ICakeConfiguration _cakeConfiguration;
        private readonly IScriptGenerationService _generationService;
        private readonly ILogger<CakeProjectSystem> _logger;
        private readonly Dictionary<string, ProjectInfo> _projects;

        public string Key => "Cake";
        public string Language => Constants.LanguageNames.Cake;
        public IEnumerable<string> Extensions => new[] { ".cake" };

        [ImportingConstructor]
        public CakeProjectSystem(
            OmniSharpWorkspace workspace,
            IOmniSharpEnvironment environment,
            ICakeConfiguration cakeConfiguration,
            IScriptGenerationService generationService,
            ILoggerFactory loggerFactory)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _cakeConfiguration = cakeConfiguration ?? throw new ArgumentNullException(nameof(cakeConfiguration));
            _generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
            _logger = loggerFactory?.CreateLogger<CakeProjectSystem>() ?? throw new ArgumentNullException(nameof(loggerFactory));

            _projects = new Dictionary<string, ProjectInfo>();
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

            // Check that bakery is installed
            var bakeryPath = CakeGenerationToolResolver.GetServerExecutablePath(_environment.TargetDirectory, _cakeConfiguration);
            if (!File.Exists(bakeryPath))
            {
                _logger.LogError("Cake.Bakery not installed");
                return;
            }

            foreach (var cakePath in allCakeFiles)
            {
                try
                {
                    var cakeScript = _generationService.Generate(new FileChange
                    {
                        FileName = cakePath,
                        FromDisk = true
                    });
                    var project = GetProject(cakeScript, cakePath);

                    // add Cake project to workspace
                    _workspace.AddProject(project);
                    var documentId = DocumentId.CreateNewId(project.Id);
                    var loader = new CakeTextLoader(cakePath, _generationService);
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

        private static ProjectInfo GetProject(CakeScript cakeScript, string filePath)
        {
            var name = Path.GetFileName(filePath);

            AssemblyLoader.LoadFrom(cakeScript.Host.AssemblyPath);

            return ProjectInfo.Create(
                id: ProjectId.CreateNewId(Guid.NewGuid().ToString()),
                version: VersionStamp.Create(),
                name: name,
                filePath: filePath,
                assemblyName: $"{name}.dll",
                language: LanguageNames.CSharp,
                compilationOptions: GetCompilationOptions(cakeScript.Usings),
                parseOptions: new CSharpParseOptions(LanguageVersion.Default, DocumentationMode.Parse, SourceCodeKind.Script),
                // TODO: Create Documentation for reference also
                metadataReferences: cakeScript.References.Select(reference => MetadataReference.CreateFromFile(reference/*, documentation: CreateDocumentationProvider(referencePath))*/)),
                // TODO: projectReferences?
                isSubmission: true,
                hostObjectType: Type.GetType(cakeScript.Host.TypeName));
        }

        private static CompilationOptions GetCompilationOptions(IEnumerable<string> usings)
        {
            var compilationOptions = new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    usings: usings,
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
