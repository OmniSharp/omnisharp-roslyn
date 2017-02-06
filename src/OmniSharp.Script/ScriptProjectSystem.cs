using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Logging;
using OmniSharp.Models.v1;
using OmniSharp.Roslyn.Models;
using OmniSharp.Services;

namespace OmniSharp.Script
{
    [Export(typeof(IProjectSystem)), Shared]
    public class ScriptProjectSystem : IProjectSystem
    {
        private readonly IMetadataFileReferenceCache _metadataFileReferenceCache;
        private CSharpParseOptions CsxParseOptions { get; } = new CSharpParseOptions(LanguageVersion.Default, DocumentationMode.Parse, SourceCodeKind.Script);
        private OmniSharpWorkspace Workspace { get; }
        private IOmniSharpEnvironment Env { get; }
        private ScriptContext Context { get; }
        private ILogger Logger { get; }

        [ImportingConstructor]
        public ScriptProjectSystem(OmniSharpWorkspace workspace, IOmniSharpEnvironment env, ILoggerFactory loggerFactory, ScriptContext scriptContext, IMetadataFileReferenceCache metadataFileReferenceCache)
        {
            _metadataFileReferenceCache = metadataFileReferenceCache;
            Workspace = workspace;
            Env = env;
            Context = scriptContext;
            Logger = loggerFactory.CreateLogger<ScriptProjectSystem>();
        }

        public string Key => "Script";
        public string Language => LanguageNames.CSharp;
        public IEnumerable<string> Extensions { get; } = new[] { ".csx" };

        public void Initalize(IConfiguration configuration)
        {
            Logger.LogInformation($"Detecting CSX files in '{Env.Path}'.");

            // Nothing to do if there are no CSX files
            var allCsxFiles = Directory.GetFiles(Env.Path, "*.csx", SearchOption.TopDirectoryOnly);
            if (allCsxFiles.Length == 0)
            {
                Logger.LogInformation("Could not find any CSX files");
                return;
            }

            Context.RootPath = Env.Path;
            Logger.LogInformation($"Found {allCsxFiles.Length} CSX files.");

            // explicitly inherit scripting library references to all global script object (InteractiveScriptGlobals) to be recognized
            var inheritedCompileLibraries = DependencyContext.Default.CompileLibraries.Where(x =>
                    x.Name.ToLowerInvariant().StartsWith("microsoft.codeanalysis")).ToList();

            // explicitly include System.ValueTuple
            inheritedCompileLibraries.AddRange(DependencyContext.Default.CompileLibraries.Where(x =>
                    x.Name.ToLowerInvariant().StartsWith("system.valuetuple")));

            var runtimeContexts = File.Exists(Path.Combine(Env.Path, "project.json")) ? ProjectContext.CreateContextForEachTarget(Env.Path) : null;

            // if we have no context, then we also have no dependencies
            // we can assume desktop framework
            // and add mscorlib
            if (runtimeContexts == null || runtimeContexts.Any() == false)
            {
                Logger.LogInformation("Unable to find project context for CSX files. Will default to non-context usage.");

                AddMetadataReference(Context.CommonReferences, typeof(object).GetTypeInfo().Assembly.Location);
                AddMetadataReference(Context.CommonReferences, typeof(Enumerable).GetTypeInfo().Assembly.Location);

                inheritedCompileLibraries.AddRange(DependencyContext.Default.CompileLibraries.Where(x =>
                        x.Name.ToLowerInvariant().StartsWith("system.runtime")));
            }
            // otherwise we will grab dependencies for the script from the runtime context
            else
            {
                // assume the first one
                var runtimeContext = runtimeContexts.First();
                Logger.LogInformation($"Found script runtime context '{runtimeContext?.TargetFramework.Framework}' for '{runtimeContext.ProjectFile.ProjectFilePath}'.");

                var projectExporter = runtimeContext.CreateExporter("Release");
                var projectDependencies = projectExporter.GetDependencies();

                // let's inject all compilation assemblies needed
                var compilationAssemblies = projectDependencies.SelectMany(x => x.CompilationAssemblies);
                foreach (var compilationAssembly in compilationAssemblies)
                {
                    Logger.LogDebug("Discovered script compilation assembly reference: " + compilationAssembly.ResolvedPath);
                    AddMetadataReference(Context.CommonReferences, compilationAssembly.ResolvedPath);
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
                Logger.LogDebug("Adding implicit reference: " + inheritedCompileLib);
                AddMetadataReference(Context.CommonReferences, inheritedCompileLib);
            }

            // Process each .CSX file
            foreach (var csxPath in allCsxFiles)
            {
                try
                {
                    var csxFileName = Path.GetFileName(csxPath);
                    var compilationOptions = new CSharpCompilationOptions(
                        outputKind: OutputKind.DynamicallyLinkedLibrary,
                        metadataReferenceResolver: ScriptMetadataResolver.Default);

                    var project = ProjectInfo.Create(
                        id: ProjectId.CreateNewId(Guid.NewGuid().ToString()),
                        version: VersionStamp.Create(),
                        name: csxFileName,
                        assemblyName: $"{csxFileName}.dll",
                        language: LanguageNames.CSharp,
                        compilationOptions: compilationOptions,
                        parseOptions: CsxParseOptions,
                        isSubmission: false,
                        hostObjectType: typeof(InteractiveScriptGlobals));

                    var scriptContext = new ScriptContext();

                    CreateCsxProject(csxPath, project, scriptContext);
                    project = project.WithMetadataReferences(Context.CommonReferences.Union(scriptContext.CsxReferences.SelectMany(x => x.Value)));

                    Workspace.AddProject(project);

                    foreach (var csxFile in scriptContext.CsxFiles)
                    {
                        Workspace.AddDocument(project.Id, csxFile, SourceCodeKind.Script);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"{csxPath} will be ignored due to the following error:", ex.ToString());
                    Logger.LogError(ex.ToString());
                    Logger.LogError(ex.InnerException?.ToString() ?? "No inner exception.");
                }
            }
        }

        private void AddMetadataReference(ISet<MetadataReference> referenceCollection, string fileReference)
        {
            if (!File.Exists(fileReference))
            {
                Logger.LogWarning($"Couldn't add reference to '{fileReference}' because the file was not found.");
                return;
            }

            var metadataReference = _metadataFileReferenceCache.GetMetadataReference(fileReference);
            if (metadataReference == null)
            {
                Logger.LogWarning($"Couldn't add reference to '{fileReference}' because the loaded metadata reference was null.");
                return;
            }

            referenceCollection.Add(metadataReference);
            Logger.LogDebug($"Added reference to '{fileReference}'");
        }

        /// <summary>
        /// Each .csx file is to be wrapped in its own project.
        /// This recursive function does a depth first traversal of the .csx files, following #load references
        /// </summary>
        private void CreateCsxProject(string csxPath, ProjectInfo project, ScriptContext scriptContext)
        {
            // Circular #load chains are not allowed
            if (scriptContext.CsxFilesBeingProcessed.Contains(csxPath))
            {
                throw new Exception($"Circular refrences among script files are not allowed: {csxPath} #loads files that end up trying to #load it again.");
            }

            // If we already have a project for this path just use that
            if (scriptContext.CsxFileProjects.ContainsKey(csxPath))
            {
                return;
            }

            Logger.LogInformation($"Processing script {csxPath}...");
            scriptContext.CsxFilesBeingProcessed.Add(csxPath);
            scriptContext.CsxFiles.Add(csxPath);

            var processResult = FileParser.ProcessFile(csxPath, CsxParseOptions);

            // CSX file usings
            scriptContext.CsxUsings[csxPath] = processResult.Namespaces.Union(Context.CommonUsings).ToList();

            // #r references
            var metadataReferencesDeclaredInCsx = new HashSet<MetadataReference>();
            foreach (var assemblyReference in processResult.References)
            {
                AddMetadataReference(metadataReferencesDeclaredInCsx, assemblyReference);
            }

            scriptContext.CsxReferences[csxPath] = metadataReferencesDeclaredInCsx;
            scriptContext.CsxLoadReferences[csxPath] =
                processResult
                    .LoadedScripts
                    .Distinct()
                    .Except(new[] {csxPath})
                    .ToList();

            foreach (var loadedScript in scriptContext.CsxLoadReferences[csxPath])
            {
                CreateCsxProject(loadedScript, project, scriptContext);
            }

            //----------LOG ONLY------------
            Logger.LogDebug($"All references by {csxPath}: \n{string.Join("\n", project.MetadataReferences.Select(r => r.Display))}");
            Logger.LogDebug($"All #load by {csxPath}: \n{string.Join("\n", scriptContext.CsxLoadReferences[csxPath])}");
            Logger.LogDebug($"All usings in {csxPath}: \n{string.Join("\n", (project.CompilationOptions as CSharpCompilationOptions)?.Usings ?? new ImmutableArray<string>())}");
            //------------------------------

            // Traversal administration
            scriptContext.CsxFileProjects[csxPath] = project;
            scriptContext.CsxFilesBeingProcessed.Remove(csxPath);
        }

        Task<object> IProjectSystem.GetProjectModelAsync(string filePath)
        {
            return Task.FromResult<object>(null);
        }

        Task<object> IProjectSystem.GetWorkspaceModelAsync(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(new ScriptContextModel(Context));
        }
    }
}
