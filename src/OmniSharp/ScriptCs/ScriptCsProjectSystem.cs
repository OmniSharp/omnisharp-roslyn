using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.Logging;
using OmniSharp.Services;
using System.Reflection;
using System.IO;
using System.Linq;

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
            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp6, DocumentationMode.Parse,
                SourceCodeKind.Script, null);
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            _logger.WriteInformation(string.Format("Detecting CSX files in '{0}'.", _env.Path));
            var csxPath = Directory.GetFiles(_env.Path, "*.csx").FirstOrDefault();
            if (csxPath != null)
            {
                _logger.WriteInformation(string.Format("Using CSX files at '{0}'.", csxPath));

                using (var stream = File.OpenRead(csxPath))
                using (var reader = new StreamReader(stream))
                {
                    var csxFileName = Path.GetFileName(csxPath);
                    var csxFile = reader.ReadToEnd();
                    var projectId = ProjectId.CreateNewId("ScriptCs");
                    var documentId = DocumentId.CreateNewId(projectId, csxFileName);

                    var mscorlib = MetadataReference.CreateFromAssembly(typeof(object).GetTypeInfo().Assembly);
                    var systemCore = MetadataReference.CreateFromAssembly(typeof(Enumerable).GetTypeInfo().Assembly);
                    var references = new[] { mscorlib, systemCore };

                    var project = ProjectInfo.Create(projectId, VersionStamp.Create(), "ScriptCs", "ScriptCs.dll", LanguageNames.CSharp, null, null,
                                                            compilationOptions, parseOptions, null, null, references, null, null, true, null); //todo add refs & host object
                    _workspace.AddProject(project);

                    var documentInfo = DocumentInfo.Create(documentId, csxFileName, null, SourceCodeKind.Script, null, csxPath)
                            //var documentInfo = DocumentInfo.Create(documentId, csxFileName)
                            .WithSourceCodeKind(SourceCodeKind.Script)
                           .WithTextLoader(TextLoader.From(TextAndVersion.Create(SourceText.From(csxFile), VersionStamp.Create())));
                    _workspace.AddDocument(documentInfo);
                }
            }
            else
            {
                _logger.WriteError("Could not find CSX files");
            }
        }
    }
}