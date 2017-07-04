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
using OmniSharp.Cake.Polyfill;
using OmniSharp.Models.WorkspaceInformation;
using OmniSharp.Services;

namespace OmniSharp.Cake
{
    [Export(typeof(IProjectSystem)), Shared]
    public class CakeProjectSystem : IProjectSystem
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly IOmniSharpEnvironment _environment;
        private readonly ILogger<CakeProjectSystem> _logger;
        private readonly IScriptGenerationService _generationService;
        private readonly Dictionary<string, ProjectInfo> _projects;

        public string Key => "Cake";
        public string Language => Constants.LanguageNames.Cake;
        public IEnumerable<string> Extensions => new[] { ".cake" };

        [ImportingConstructor]
        public CakeProjectSystem(
            OmniSharpWorkspace workspace,
            IOmniSharpEnvironment environment,
            ILoggerFactory loggerFactory,
            IScriptGenerationService generationService)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = loggerFactory?.CreateLogger<CakeProjectSystem>() ?? throw new ArgumentNullException(nameof(loggerFactory));
            _generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));

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
            throw new NotImplementedException();
        }

        public Task<object> GetProjectModelAsync(string filePath)
        {
            throw new NotImplementedException();
        }

        private ProjectInfo GetProject(CakeScript cakeScript, string filePath)
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

        private CompilationOptions GetCompilationOptions(ISet<string> usings)
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
