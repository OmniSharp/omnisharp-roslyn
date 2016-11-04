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
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Logging;
using OmniSharp.Models.v1;
using OmniSharp.Services;

namespace OmniSharp.Script
{
    [Export(typeof(IProjectSystem))]
    public class ScriptProjectSystem : IProjectSystem
    {
        private CSharpParseOptions CsxParseOptions { get; } = new CSharpParseOptions(LanguageVersion.CSharp6, DocumentationMode.Parse, SourceCodeKind.Script);
        private OmnisharpWorkspace Workspace { get; }
        private IOmnisharpEnvironment Env { get; }
        private ScriptContext Context { get; }
        private ILogger Logger { get; }

        [ImportingConstructor]
        public ScriptProjectSystem(OmnisharpWorkspace workspace, IOmnisharpEnvironment env, ILoggerFactory loggerFactory, ScriptContext scriptContext)
        {
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
            var allCsxFiles = Directory.GetFiles(Env.Path, "*.csx", SearchOption.AllDirectories);
            if (allCsxFiles.Length == 0)
            {
                Logger.LogInformation("Could not find any CSX files");
                return;
            }

            Context.RootPath = Env.Path;
            Logger.LogInformation($"Found {allCsxFiles.Length} CSX files.");

            var runtimeContext = ProjectContext.CreateContextForEachTarget(Env.Path).First();
            Logger.LogInformation($"Found script runtime context for '{runtimeContext.ProjectFile.ProjectFilePath}'");

            var projectExporter = runtimeContext.CreateExporter("Release");
            var projectDependencies = projectExporter.GetDependencies();

            foreach (var compilationAssembly in projectDependencies.SelectMany(x => x.CompilationAssemblies))
            {
                Logger.LogDebug("Discovered script compilation assembly reference: " + compilationAssembly.ResolvedPath);
                Context.CommonReferences.Add(MetadataReference.CreateFromFile(compilationAssembly.ResolvedPath));
            }

            var needsSystemRuntimeReference = Context.CommonReferences.Any(x => !x.Display.ToLowerInvariant().EndsWith("system.runtime.dll"));
            var inheritedCompileLibraries = needsSystemRuntimeReference ? 
                DependencyContext.Default.CompileLibraries.Where(x => x.Name.ToLowerInvariant().StartsWith("microsoft.codeanalysis") || x.Name.ToLowerInvariant().StartsWith("system.runtime")) : 
                DependencyContext.Default.CompileLibraries.Where(x => x.Name.ToLowerInvariant().StartsWith("microsoft.codeanalysis"));

            foreach (var inheritedCompileLib in inheritedCompileLibraries.SelectMany(x => x.ResolveReferencePaths()))
            {
                Logger.LogDebug("Adding implicit reference: " + inheritedCompileLib);
                Context.CommonReferences.Add(MetadataReference.CreateFromFile(inheritedCompileLib));
            }

            // Process each .CSX file
            foreach (var csxPath in allCsxFiles)
            {
                try
                {
                    CreateCsxProject(csxPath);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"{csxPath} will be ignored due to the following error:", ex.ToString());
                    Logger.LogError(ex.ToString());
                    Logger.LogError(ex.InnerException?.ToString() ?? "No inner exception.");
                }
            }
        }

        /// <summary>
        /// Each .csx file is to be wrapped in its own project.
        /// This recursive function does a depth first traversal of the .csx files, following #load references
        /// </summary>
        private ProjectInfo CreateCsxProject(string csxPath)
        {
            // Circular #load chains are not allowed
            if (Context.CsxFilesBeingProcessed.Contains(csxPath))
            {
                throw new Exception($"Circular refrences among script files are not allowed: {csxPath} #loads files that end up trying to #load it again.");
            }

            // If we already have a project for this path just use that
            if (Context.CsxFileProjects.ContainsKey(csxPath))
            {
                return Context.CsxFileProjects[csxPath];
            }

            Logger.LogInformation($"Processing script {csxPath}...");
            Context.CsxFilesBeingProcessed.Add(csxPath);

            var fileParser = new FileParser(Context.RootPath);
            var processResult = fileParser.ProcessFile(csxPath);

            // CSX file usings
            Context.CsxUsings[csxPath] = processResult.Namespaces.Union(Context.CommonUsings).ToList();

            var compilationOptions = new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                usings: Context.CsxUsings[csxPath]);

            // #r refernces
            Context.CsxReferences[csxPath] = processResult.References.Select(x => MetadataReference.CreateFromFile(x)).ToList();

            Context.CsxLoadReferences[csxPath] =
                processResult
                    .LoadedScripts
                    .Distinct()
                    .Except(new[] {csxPath})
                    .Select(loadedCsxPath => CreateCsxProject(loadedCsxPath))
                    .ToList();

            // Create the wrapper project and add it to the workspace
            Logger.LogDebug($"Creating project for script {csxPath}.");
            var csxFileName = Path.GetFileName(csxPath);
            var project = ProjectInfo.Create(
                id: ProjectId.CreateNewId(Guid.NewGuid().ToString()),
                version: VersionStamp.Create(),
                name: csxFileName,
                assemblyName: $"{csxFileName}.dll",
                language: LanguageNames.CSharp,
                compilationOptions: compilationOptions,
                parseOptions: CsxParseOptions,
                metadataReferences: Context.CommonReferences.Union(Context.CsxReferences[csxPath]),
                projectReferences: Context.CsxLoadReferences[csxPath].Select(p => new ProjectReference(p.Id)),
                isSubmission: true,
                hostObjectType: typeof(InteractiveScriptGlobals));
            Workspace.AddProject(project);
            AddFile(csxPath, project.Id);

            //----------LOG ONLY------------
            Logger.LogDebug($"All references by {csxFileName}: \n{string.Join("\n", project.MetadataReferences.Select(r => r.Display))}");
            Logger.LogDebug($"All #load projects by {csxFileName}: \n{string.Join("\n", Context.CsxLoadReferences[csxPath].Select(p => p.Name))}");
            Logger.LogDebug($"All usings in {csxFileName}: \n{string.Join("\n", (project.CompilationOptions as CSharpCompilationOptions)?.Usings ?? new ImmutableArray<string>())}");
            //------------------------------

            // Traversal administration
            Context.CsxFileProjects[csxPath] = project;
            Context.CsxFilesBeingProcessed.Remove(csxPath);

            return project;
        }

        private void AddFile(string filePath, ProjectId projectId)
        {
            using (var stream = File.OpenRead(filePath))
            using (var reader = new StreamReader(stream))
            {
                var fileName = Path.GetFileName(filePath);
                var csxFile = reader.ReadToEnd();

                var documentId = DocumentId.CreateNewId(projectId, fileName);
                var documentInfo = DocumentInfo.Create(documentId, fileName, null, SourceCodeKind.Script, null, filePath)
                    .WithSourceCodeKind(SourceCodeKind.Script)
                    .WithTextLoader(TextLoader.From(TextAndVersion.Create(SourceText.From(csxFile), VersionStamp.Create())));
                Workspace.AddDocument(documentInfo);
            }
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
