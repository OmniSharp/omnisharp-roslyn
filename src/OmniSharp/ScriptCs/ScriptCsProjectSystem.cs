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

namespace OmniSharp.ScriptCs
{
    public class ScriptCsProjectSystem : IProjectSystem
    {
        private readonly OmnisharpWorkspace _workspace;
        private readonly IOmnisharpEnvironment _env;
        private readonly ILogger _logger;

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

                var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp6, DocumentationMode.Parse,
    SourceCodeKind.Script, null);
                var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

                var fs = new FileSystem();
                var scriptCsPreProcessor = new FilePreProcessor(fs, LogManager.GetCurrentClassLogger(), new ILineProcessor[] {
                        new LoadLineProcessor(fs),
                        new UsingLineProcessor(),
                        new ReferenceLineProcessor(fs)
                    });

                var processResult = scriptCsPreProcessor.ProcessFile(csxPath);

                var mscorlib = MetadataReference.CreateFromAssembly(typeof(object).GetTypeInfo().Assembly);
                var systemCore = MetadataReference.CreateFromAssembly(typeof(Enumerable).GetTypeInfo().Assembly);
                var references = new List<MetadataReference> { mscorlib, systemCore };

                var baseAssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
                foreach (var importedReference in processResult.References)
                {
                    if (fs.FileExists(importedReference))
                    {
                        references.Add(MetadataReference.CreateFromFile(importedReference));
                    }
                    else
                    {
                        references.Add(MetadataReference.CreateFromFile(Path.Combine(baseAssemblyPath, importedReference.ToLower().EndsWith(".dll") ? importedReference : importedReference + ".dll")));
                    }
                }

                var projectId = ProjectId.CreateNewId("ScriptCs");
                var project = ProjectInfo.Create(projectId, VersionStamp.Create(), "ScriptCs", "ScriptCs.dll", LanguageNames.CSharp, null, null,
                                                        compilationOptions, parseOptions, null, null, references, null, null, true, null); //todo add refs & host object
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