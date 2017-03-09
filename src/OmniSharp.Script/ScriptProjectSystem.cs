using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Logging;
using OmniSharp.Models.v1;
using OmniSharp.Services;

namespace OmniSharp.Script
{
    [Export(typeof(IProjectSystem)), Shared]
    public class ScriptProjectSystem : IProjectSystem
    {
        // aligned with CSI.exe
        // https://github.com/dotnet/roslyn/blob/version-2.0.0-rc3/src/Interactive/csi/csi.rsp
        internal static readonly IEnumerable<string> DefaultNamespaces = new[]
        {
            "System",
            "System.IO",
            "System.Collections.Generic",
            "System.Console",
            "System.Diagnostics",
            "System.Dynamic",
            "System.Linq",
            "System.Linq.Expressions",
            "System.Text",
            "System.Threading.Tasks"
        };

        private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Default, DocumentationMode.Parse, SourceCodeKind.Script);

        private static readonly Lazy<CSharpCompilationOptions> CompilationOptions = new Lazy<CSharpCompilationOptions>(() =>
        {
            var compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                usings: DefaultNamespaces,
                allowUnsafe: true,
                metadataReferenceResolver: ScriptMetadataResolver.Default,
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
        });

        private readonly IMetadataFileReferenceCache _metadataFileReferenceCache;

        // used for tracking purposes only
        private readonly HashSet<string> _assemblyReferences = new HashSet<string>();

        private readonly Dictionary<string, ProjectInfo> _projects;
        private readonly OmniSharpWorkspace _workspace;
        private readonly IOmniSharpEnvironment _env;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public ScriptProjectSystem(OmniSharpWorkspace workspace, IOmniSharpEnvironment env, ILoggerFactory loggerFactory, IMetadataFileReferenceCache metadataFileReferenceCache)
        {
            _metadataFileReferenceCache = metadataFileReferenceCache;
            _workspace = workspace;
            _env = env;
            _logger = loggerFactory.CreateLogger<ScriptProjectSystem>();
            _projects = new Dictionary<string, ProjectInfo>();
        }

        public string Key => "Script";
        public string Language => LanguageNames.CSharp;
        public IEnumerable<string> Extensions => new[] { ".csx" };

        public void Initalize(IConfiguration configuration)
        {
            _logger.LogInformation($"Detecting CSX files in '{_env.Path}'.");

            // Nothing to do if there are no CSX files
            var allCsxFiles = Directory.GetFiles(_env.Path, "*.csx", SearchOption.AllDirectories);
            if (allCsxFiles.Length == 0)
            {
                _logger.LogInformation("Could not find any CSX files");
                return;
            }

            _logger.LogInformation($"Found {allCsxFiles.Length} CSX files.");

            // explicitly inherit scripting library references to all global script object (InteractiveScriptGlobals) to be recognized
            var inheritedCompileLibraries = DependencyContext.Default.CompileLibraries.Where(x =>
                    x.Name.ToLowerInvariant().StartsWith("microsoft.codeanalysis")).ToList();

            // explicitly include System.ValueTuple
            inheritedCompileLibraries.AddRange(DependencyContext.Default.CompileLibraries.Where(x =>
                    x.Name.ToLowerInvariant().StartsWith("system.valuetuple")));

            var runtimeContexts = File.Exists(Path.Combine(_env.Path, "project.json")) ? ProjectContext.CreateContextForEachTarget(_env.Path) : null;

            var commonReferences = new HashSet<MetadataReference>();

            // if we have no context, then we also have no dependencies
            // we can assume desktop framework
            // and add mscorlib
            if (runtimeContexts == null || runtimeContexts.Any() == false)
            {
                _logger.LogInformation("Unable to find project context for CSX files. Will default to non-context usage.");

                AddMetadataReference(commonReferences, typeof(object).GetTypeInfo().Assembly.Location);
                AddMetadataReference(commonReferences, typeof(Enumerable).GetTypeInfo().Assembly.Location);

                inheritedCompileLibraries.AddRange(DependencyContext.Default.CompileLibraries.Where(x =>
                        x.Name.ToLowerInvariant().StartsWith("system.runtime")));
            }
            // otherwise we will grab dependencies for the script from the runtime context
            else
            {
                // assume the first one
                var runtimeContext = runtimeContexts.First();
                _logger.LogInformation($"Found script runtime context '{runtimeContext?.TargetFramework.Framework}' for '{runtimeContext.ProjectFile.ProjectFilePath}'.");

                var projectExporter = runtimeContext.CreateExporter("Release");
                var projectDependencies = projectExporter.GetDependencies();

                // let's inject all compilation assemblies needed
                var compilationAssemblies = projectDependencies.SelectMany(x => x.CompilationAssemblies);
                foreach (var compilationAssembly in compilationAssemblies)
                {
                    _logger.LogDebug("Discovered script compilation assembly reference: " + compilationAssembly.ResolvedPath);
                    AddMetadataReference(commonReferences, compilationAssembly.ResolvedPath);
                }

                // for non .NET Core, include System.Runtime
                if (runtimeContext.TargetFramework.Framework != ".NETCoreApp")
                {

                    inheritedCompileLibraries.AddRange(DependencyContext.Default.CompileLibraries.Where(x =>
                            x.Name.ToLowerInvariant().StartsWith("system.runtime")));
                }
            }

            // inject all inherited assemblies
            foreach (var inheritedCompileLib in inheritedCompileLibraries.SelectMany(x => x.ResolveReferencePaths()))
            {
                _logger.LogDebug("Adding implicit reference: " + inheritedCompileLib);
                AddMetadataReference(commonReferences, inheritedCompileLib);
            }

            // Each .CSX file becomes an entry point for it's own project
            // Every #loaded file will be part of the project too
            foreach (var csxPath in allCsxFiles)
            {
                try
                {
                    var csxFileName = Path.GetFileName(csxPath);
                    var project = ProjectInfo.Create(
                        id: ProjectId.CreateNewId(),
                        version: VersionStamp.Create(),
                        name: csxFileName,
                        assemblyName: $"{csxFileName}.dll",
                        language: LanguageNames.CSharp,
                        compilationOptions: CompilationOptions.Value,
                        metadataReferences: commonReferences,
                        parseOptions: ParseOptions,
                        isSubmission: true,
                        hostObjectType: typeof(InteractiveScriptGlobals));

                    // add CSX project to workspace
                    _workspace.AddProject(project);
                    _workspace.AddDocument(project.Id, csxPath, SourceCodeKind.Script);
                    _projects[csxPath] = project;
                    _logger.LogInformation($"Added CSX project '{csxPath}' to the workspace.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{csxPath} will be ignored due to an following error");
                }
            }
        }

        private void AddMetadataReference(ISet<MetadataReference> referenceCollection, string fileReference)
        {
            if (!File.Exists(fileReference))
            {
                _logger.LogWarning($"Couldn't add reference to '{fileReference}' because the file was not found.");
                return;
            }

            var metadataReference = _metadataFileReferenceCache.GetMetadataReference(fileReference);
            if (metadataReference == null)
            {
                _logger.LogWarning($"Couldn't add reference to '{fileReference}' because the loaded metadata reference was null.");
                return;
            }

            referenceCollection.Add(metadataReference);
            _assemblyReferences.Add(fileReference);
            _logger.LogDebug($"Added reference to '{fileReference}'");
        }

        private ProjectInfo GetProjectFileInfo(string path)
        {
            ProjectInfo projectFileInfo;
            if (!_projects.TryGetValue(path, out projectFileInfo))
            {
                return null;
            }

            return projectFileInfo;
        }

        Task<object> IProjectSystem.GetProjectModelAsync(string filePath)
        {
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

            return Task.FromResult<object>(new ScriptContextModel(filePath, projectInfo, _assemblyReferences));
        }

        Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            var scriptContextModels = new List<ScriptContextModel>();
            foreach (var project in _projects)
            {
                scriptContextModels.Add(new ScriptContextModel(project.Key, project.Value, _assemblyReferences));
            }
            return Task.FromResult<object>(new ScriptContextModelCollection(scriptContextModels));
        }
    }
}
