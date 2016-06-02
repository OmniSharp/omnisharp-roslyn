using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Common.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp.Models.v1;
using OmniSharp.Services;
using ScriptCs;
using ScriptCs.Contracts;
using LogLevel = ScriptCs.Contracts.LogLevel;
using ScriptCs.Hosting;
using OmniSharp.ScriptCs.Extensions;
using System.Collections.Immutable;

namespace OmniSharp.ScriptCs
{
    [Export(typeof(IProjectSystem))]
    public class ScriptCsProjectSystem : IProjectSystem
    {
        private static readonly string BaseAssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
        OmnisharpWorkspace Workspace { get; }
        IOmnisharpEnvironment Env { get; }
        ScriptCsContext Context { get; }
        ILogger Logger { get; }

        CSharpParseOptions CsxParseOptions { get; } = new CSharpParseOptions(LanguageVersion.CSharp6, DocumentationMode.Parse, SourceCodeKind.Script);

        IEnumerable<MetadataReference> DotNetBaseReferences { get; } = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),        // mscorlib
            MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location),    // systemCore 
            MetadataReference.CreateFromFile(typeof(IScriptHost).Assembly.Location)                  // scriptcsContracts 
        };



        [ImportingConstructor]
        public ScriptCsProjectSystem(OmnisharpWorkspace workspace, IOmnisharpEnvironment env, ILoggerFactory loggerFactory, ScriptCsContext scriptCsContext)
        {
            Workspace = workspace;
            Env = env;
            Context = scriptCsContext;
            Logger = loggerFactory.CreateLogger<ScriptCsProjectSystem>();
        }

        public string Key { get { return "ScriptCs"; } }
        public string Language { get { return LanguageNames.CSharp; } }
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

            // TODO: write and adapter to implement the new ScriptCs ILogProvider interface
            #pragma warning disable 0618
            //script name is added here as a fake one (dir path not even a real file); this is OK though -> it forces MEF initialization
            var baseScriptServicesBuilder = new ScriptServicesBuilder(new ScriptConsole(), LogManager.GetCurrentClassLogger())
                    .LogLevel(LogLevel.Debug)
                    .Cache(false)
                    .Repl(false)
                    .ScriptName(Env.Path)
                    .ScriptEngine<NullScriptEngine>();

            var scriptServices = baseScriptServicesBuilder.Build();

            var scriptPacks = scriptServices.ScriptPackResolver.GetPacks().ToList();
            var assemblyPaths = scriptServices.AssemblyResolver.GetAssemblyPaths(Env.Path);

            // Common usings and references
            Context.CommonUsings.UnionWith(ScriptExecutor.DefaultNamespaces);

            Context.CommonReferences.UnionWith(DotNetBaseReferences);
            Context.CommonReferences.UnionWith(ScriptExecutor.DefaultReferences.ToMetadataReferences(scriptServices));       // ScriptCs defaults
            Context.CommonReferences.UnionWith(assemblyPaths.ToMetadataReferences(scriptServices));                        // nuget references

            if (scriptPacks != null && scriptPacks.Any())
            {
                var scriptPackSession = new ScriptPackSession(scriptPacks, new string[0]);
                scriptPackSession.InitializePacks();

                //script pack references
                Context.CommonReferences.UnionWith(scriptPackSession.References.ToMetadataReferences(scriptServices));

                //script pack usings
                Context.CommonUsings.UnionWith(scriptPackSession.Namespaces);

                Context.ScriptPacks.UnionWith(scriptPackSession.Contexts.Select(pack => pack.GetType().ToString()));
            }

            // Process each .CSX file
            foreach (var csxPath in allCsxFiles)
            {
                try
                {
                    CreateCsxProject(csxPath, baseScriptServicesBuilder);
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
        /// This recursive function will do depth first traversal of the .csx files, following #load references
        /// </summary>
        private ProjectInfo CreateCsxProject(string csxPath, IScriptServicesBuilder baseBuilder)
        {
            // We are supposed to do a depth first traversal through #load references
            if(Context.CsxFilesBeingProcessed.Contains(csxPath))
            {
                throw new Exception($"Circular refrences among script files are not allowed: {csxPath} #loads files that end up trying to #load it again.");
            }

            // If we already have a project for this path just use that
            if(Context.CsxFileProjects.ContainsKey(csxPath))
            {
                return Context.CsxFileProjects[csxPath];
            }

            // Process the file with ScriptCS first
            Logger.LogInformation($"Processing script {csxPath}...");
            Context.CsxFilesBeingProcessed.Add(csxPath);
            var localScriptServices = baseBuilder.ScriptName(csxPath).Build();
            var processResult = localScriptServices.FilePreProcessor.ProcessFile(csxPath);

            // CSX file usings
            Context.CsxUsings[csxPath] = processResult.Namespaces.ToList();

            var compilationOptions = new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                usings: Context.CommonUsings.Union(Context.CsxUsings[csxPath]));

            // #r refernces
            Context.CsxReferences[csxPath] = processResult.References.ToMetadataReferences(localScriptServices).ToList();

            //#load references recursively
            Context.CsxLoadReferences[csxPath] = 
                processResult
                    .LoadedScripts
                    .Distinct()
                    .Except(new[] { csxPath })
                    .Select(loadedCsxPath => CreateCsxProject(loadedCsxPath, baseBuilder))
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
                hostObjectType: typeof(IScriptHost));
            Workspace.AddProject(project);
            AddFile(csxPath, project.Id);

            //----------LOG ONLY------------
            var metadataReferences = Context.CommonReferences.Union(Context.CsxReferences[csxPath]).Union(Context.CsxLoadReferences[csxPath].SelectMany(p => p.MetadataReferences));
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

        Task<object> IProjectSystem.GetProjectModel(string path)
        {
            return Task.FromResult<object>(null);
        }

        Task<object> IProjectSystem.GetInformationModel(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(Context);
        }
    }
}
