#if ASPNET50
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Logging;
using OmniSharp.Services;
using System.Reflection;
using System.IO;
using System.Linq;
using ScriptCs;
using Common.Logging;
using ScriptCs.Contracts;
using System.Collections.Generic;
using ScriptCs.Hosting;
using LogLevel = ScriptCs.Contracts.LogLevel;
using System;
using Common.Logging.Simple;
using Autofac;

namespace OmniSharp.ScriptCs
{
    public class NullScriptEngine : IScriptEngine
    {
        public string BaseDirectory
        {
            get;

            set;
        }

        public string CacheDirectory
        {
            get;

            set;
        }

        public string FileName
        {
            get;
            set;
        }

        public ScriptResult Execute(string code, string[] scriptArgs, AssemblyReferences references, IEnumerable<string> namespaces, ScriptPackSession scriptPackSession)
        {
            return new ScriptResult();
        }
    }

    public class ScriptCsProjectSystem : IProjectSystem
    {
        private static string baseAssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
        private readonly OmnisharpWorkspace _workspace;
        private readonly IOmnisharpEnvironment _env;
        private readonly ILogger _logger;
        private ScriptServices _scriptServices;

        public ScriptCsProjectSystem(OmnisharpWorkspace workspace, IOmnisharpEnvironment env, ILoggerFactory loggerFactory)
        {
            _workspace = workspace;
            _env = env;
            _logger = loggerFactory.Create<ScriptCsProjectSystem>();
        }

        public void Initalize()
        {
            _logger.WriteInformation(string.Format("Detecting CSX files in '{0}'.", _env.Path));

            //temp hack, only work with files like foo.csx - starting with F
            var csxPath = Directory.GetFiles(_env.Path, "*.csx").FirstOrDefault(x => Path.GetFileName(x).StartsWith("f"));
            if (csxPath != null)
            {
                _logger.WriteInformation(string.Format("Using CSX files at '{0}'.", csxPath));

                LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter();
                var scriptcsLogger = LogManager.GetCurrentClassLogger();

                //todo: initialize assembly redirects or not?
                //var initializationServices = new InitializationServices(scriptcsLogger);
                //initializationServices.GetAppDomainAssemblyResolver().Initialize();

                var scriptServicesBuilder = new ScriptServicesBuilder(new ScriptConsole(), scriptcsLogger).
                    LogLevel(LogLevel.Info).Cache(false).Repl(false).ScriptName(csxPath).ScriptEngine<NullScriptEngine>();

                _scriptServices = scriptServicesBuilder.Build();

                var mscorlib = MetadataReference.CreateFromAssembly(typeof(object).GetTypeInfo().Assembly);
                var systemCore = MetadataReference.CreateFromAssembly(typeof(Enumerable).GetTypeInfo().Assembly);
                var scriptcsContracts = MetadataReference.CreateFromAssembly(typeof(IScriptHost).Assembly);
                var references = new List<MetadataReference> { mscorlib, systemCore, scriptcsContracts };
                var usings = new List<string>();

                var processResult = _scriptServices.FilePreProcessor.ProcessFile(csxPath);

                //file usings
                usings.AddRange(processResult.Namespaces);

                //#r references
                ImportReferences(references, processResult.References);

                var assemblyPaths = _scriptServices.AssemblyResolver.GetAssemblyPaths(_env.Path);
                //nuget references
                ImportReferences(references, assemblyPaths);

                //script packs
                var scriptPacks = _scriptServices.ScriptPackResolver.GetPacks().ToList();

                //hack: alternative to use if we have problems with scriptcs.contracts versions
                //var scriptPackTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).
                //    Where(t => t.GetInterfaces().Any(i => i.FullName == "ScriptCs.Contracts.IScriptPack")).ToArray();

                //usings.AddRange(scriptPackTypes.Select(x => x.Namespace));

                //foreach (var scriptPackType in scriptPackTypes)
                //{
                //    try
                //    {
                //        scriptPacks.Add(Activator.CreateInstance(scriptPackType) as IScriptPack);
                //    }
                //    catch (Exception e)
                //    {
                //        scriptcsLogger.Error("Error activiatig script pack", e);
                //    }
                //}

                if (scriptPacks != null && scriptPacks.Any())
                {
                    var scriptPackSession = new ScriptPackSession(scriptPacks, new string[0]);
                    scriptPackSession.InitializePacks();

                    //script pack references
                    ImportReferences(references, scriptPackSession.References);

                    //script pack usings
                    usings.AddRange(scriptPackSession.Namespaces);
                }

                var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp6, DocumentationMode.Parse,
SourceCodeKind.Script);
                var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, usings: usings.Distinct());

                var projectId = ProjectId.CreateNewId("ScriptCs");
                var project = ProjectInfo.Create(projectId, VersionStamp.Create(), "ScriptCs", "ScriptCs.dll", LanguageNames.CSharp, null, null,
                                                        compilationOptions, parseOptions, null, null, references, null, null, true, typeof(IScriptHost)); //todo add refs & host object
                _workspace.AddProject(project);

                AddFile(csxPath, projectId);

                //foreach (var filePath in processResult.LoadedScripts.Distinct())
                //{
                //    AddFile(filePath, projectId);
                //}
            }
            else
            {
                _logger.WriteError("Could not find CSX files");
            }
        }

        private void ImportReferences(List<MetadataReference> listOfReferences, IEnumerable<string> referencesToImport)
        {
            foreach (var importedReference in referencesToImport)
            {
                if (_scriptServices.FileSystem.IsPathRooted(importedReference))
                {
                    if (_scriptServices.FileSystem.FileExists(importedReference))
                        listOfReferences.Add(MetadataReference.CreateFromFile(importedReference));
                }
                else
                {
                    listOfReferences.Add(MetadataReference.CreateFromFile(Path.Combine(baseAssemblyPath, importedReference.ToLower().EndsWith(".dll") ? importedReference : importedReference + ".dll")));
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
    }
}
#endif