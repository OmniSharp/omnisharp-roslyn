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
using Microsoft.Framework.Configuration;
using Microsoft.Framework.Logging;
using OmniSharp.Models.v1;
using OmniSharp.Services;
using ScriptCs;
using ScriptCs.Contracts;
using LogLevel = ScriptCs.Contracts.LogLevel;
using ScriptCs.Hosting;

namespace OmniSharp.ScriptCs
{
    [Export(typeof(IProjectSystem))]
    public class ScriptCsProjectSystem : IProjectSystem
    {
        private static readonly string BaseAssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
        private readonly OmnisharpWorkspace _workspace;
        private readonly IOmnisharpEnvironment _env;
        private readonly ScriptCsContext _scriptCsContext;
        private readonly ILogger _logger;
        private ScriptServices _scriptServices;

        [ImportingConstructor]
        public ScriptCsProjectSystem(OmnisharpWorkspace workspace, IOmnisharpEnvironment env, ILoggerFactory loggerFactory, ScriptCsContext scriptCsContext)
        {
            _workspace = workspace;
            _env = env;
            _scriptCsContext = scriptCsContext;
            _logger = loggerFactory.CreateLogger<ScriptCsProjectSystem>();
        }

        public string Key { get { return "ScriptCs"; } }
        public string Language { get { return LanguageNames.CSharp; } }
        public IEnumerable<string> Extensions { get; } = new[] { ".csx" };

        public void Initalize(IConfiguration configuration)
        {
            _logger.LogInformation($"Detecting CSX files in '{_env.Path}'.");

            var allCsxFiles = Directory.GetFiles(_env.Path, "*.csx", SearchOption.TopDirectoryOnly);

            if (allCsxFiles.Length == 0)
            {
                _logger.LogInformation("Could not find any CSX files");
                return;
            }

            _scriptCsContext.Path = _env.Path;
            _logger.LogInformation($"Found {allCsxFiles.Length} CSX files.");

            //script name is added here as a fake one (dir path not even a real file); this is OK though -> it forces MEF initialization
            var scriptServicesBuilder = new ScriptServicesBuilder(new ScriptConsole(), LogManager.GetCurrentClassLogger()).
                LogLevel(LogLevel.Info).Cache(false).Repl(false).ScriptName(_env.Path).ScriptEngine<NullScriptEngine>();

            _scriptServices = scriptServicesBuilder.Build();

            var mscorlib = MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location);
            var systemCore = MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location);
            var scriptcsContracts = MetadataReference.CreateFromFile(typeof(IScriptHost).Assembly.Location);

            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp6, DocumentationMode.Parse, SourceCodeKind.Script);

            var scriptPacks = _scriptServices.ScriptPackResolver.GetPacks().ToList();
            var assemblyPaths = _scriptServices.AssemblyResolver.GetAssemblyPaths(_env.Path);

            foreach (var csxPath in allCsxFiles)
            {
                try
                {
                    _scriptCsContext.CsxFiles.Add(csxPath);
                    var processResult = _scriptServices.FilePreProcessor.ProcessFile(csxPath);

                    var references = new List<MetadataReference> { mscorlib, systemCore, scriptcsContracts };
                    var usings = new List<string>(ScriptExecutor.DefaultNamespaces);

                    //default references
                    ImportReferences(references, ScriptExecutor.DefaultReferences);

                    //file usings
                    usings.AddRange(processResult.Namespaces);

                    //#r references
                    ImportReferences(references, processResult.References);

                    //nuget references
                    ImportReferences(references, assemblyPaths);

                    //script packs
                    if (scriptPacks != null && scriptPacks.Any())
                    {
                        var scriptPackSession = new ScriptPackSession(scriptPacks, new string[0]);
                        scriptPackSession.InitializePacks();

                        //script pack references
                        ImportReferences(references, scriptPackSession.References);

                        //script pack usings
                        usings.AddRange(scriptPackSession.Namespaces);

                        _scriptCsContext.ScriptPacks.UnionWith(scriptPackSession.Contexts.Select(pack => pack.GetType().ToString()));
                    }

                    _scriptCsContext.References.UnionWith(references.Select(x => x.Display));
                    _scriptCsContext.Usings.UnionWith(usings);

                    var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, usings: usings.Distinct());

                    var fileName = Path.GetFileName(csxPath);

                    var projectId = ProjectId.CreateNewId(Guid.NewGuid().ToString());
                    var project = ProjectInfo.Create(projectId, VersionStamp.Create(), fileName, $"{fileName}.dll", LanguageNames.CSharp, null, null,
                                                            compilationOptions, parseOptions, null, null, references, null, null, true, typeof(IScriptHost));

                    _workspace.AddProject(project);
                    AddFile(csxPath, projectId);

                    foreach (var filePath in processResult.LoadedScripts.Distinct().Except(new[] { csxPath }))
                    {
                        _scriptCsContext.CsxFiles.Add(filePath);
                        var loadedFileName = Path.GetFileName(filePath);

                        var loadedFileProjectId = ProjectId.CreateNewId(Guid.NewGuid().ToString());
                        var loadedFileSubmissionProject = ProjectInfo.Create(loadedFileProjectId, VersionStamp.Create(),
                            $"{loadedFileName}-LoadedFrom-{fileName}", $"{loadedFileName}-LoadedFrom-{fileName}.dll", LanguageNames.CSharp, null, null,
                                            compilationOptions, parseOptions, null, null, references, null, null, true, typeof(IScriptHost));

                        _workspace.AddProject(loadedFileSubmissionProject);
                        AddFile(filePath, loadedFileProjectId);
                        _workspace.AddProjectReference(projectId, new ProjectReference(loadedFileProjectId));
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError($"{csxPath} will be ignored due to the following error:", ex);
                }
            }
        }

        private void ImportReferences(List<MetadataReference> listOfReferences, IEnumerable<string> referencesToImport)
        {
            foreach (var importedReference in referencesToImport.Where(x => !x.ToLowerInvariant().Contains("scriptcs.contracts")))
            {
                if (_scriptServices.FileSystem.IsPathRooted(importedReference))
                {
                    if (_scriptServices.FileSystem.FileExists(importedReference))
                        listOfReferences.Add(MetadataReference.CreateFromFile(importedReference));
                }
                else
                {
                    listOfReferences.Add(MetadataReference.CreateFromFile(Path.Combine(BaseAssemblyPath, importedReference.ToLower().EndsWith(".dll") ? importedReference : importedReference + ".dll")));
                }
            }
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
                _workspace.AddDocument(documentInfo);
            }
        }

        Task<object> IProjectSystem.GetProjectModel(string path)
        {
            return Task.FromResult<object>(null);
        }

        Task<object> IProjectSystem.GetInformationModel(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(_scriptCsContext);
        }
    }
}
