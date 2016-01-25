using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;

namespace OmniSharp.Tests
{
    public class TestAssistant
    {
        private readonly ILogger<TestAssistant> _logger;

        public TestAssistant()
        {
            LoggerFactory.AddConsole();
            _logger = LoggerFactory.CreateLogger<TestAssistant>();
        }

        public LoggerFactory LoggerFactory { get; } = new LoggerFactory();

        public OmnisharpWorkspace CreateWorkspace(string sourceCode)
        {
            _logger.LogInformation("Create simple workspace.");
            _logger.LogInformation("Source code: \n" + sourceCode);

            return CreateWorkspace(new Dictionary<string, string> { { "Dummy.cs", sourceCode } });
        }

        public OmnisharpWorkspace CreateWorkspace(Dictionary<string, string> sourceFiles)
        {
            _logger.LogInformation("Create simple workspace.");
            if (sourceFiles.Any())
            {
                _logger.LogInformation($"  Source files: {string.Join(",", sourceFiles.Keys)}");
            }

            return CreateWorkspace(CreatePluginHost(), sourceFiles);
        }

        public void AddProjectToWorkspace(OmnisharpWorkspace workspace,
                                          string projectFilePath,
                                          IEnumerable<string> frameworks,
                                          IDictionary<string, string> sourceFiles)
        {
            _logger.LogInformation($"  AddProjectToWorkspace");

            var versionStamp = VersionStamp.Create();
            var references = new[]
            {
                CreateFromType(typeof(object)),     // mscorlib
                CreateFromType(typeof(Enumerable))  // System.Core
            };

            foreach (var framework in frameworks)
            {
                var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(),
                                                     versionStamp,
                                                     $"OmniSharp+{framework}",
                                                     "AssemblyName",
                                                     LanguageNames.CSharp,
                                                     projectFilePath,
                                                     metadataReferences: references);

                workspace.AddProject(projectInfo);
                foreach (var file in sourceFiles)
                {
                    var document = DocumentInfo.Create(DocumentId.CreateNewId(projectInfo.Id),
                                                       file.Key,
                                                       folders: null,
                                                       sourceCodeKind: SourceCodeKind.Regular,
                                                       loader: TextLoader.From(TextAndVersion.Create(SourceText.From(file.Value), versionStamp)),
                                                       filePath: file.Key);

                    workspace.AddDocument(document);
                }
            }
        }

        private OmnisharpWorkspace CreateWorkspace(
            CompositionHost compositionHost,
            Dictionary<string, string> sourceFiles)
        {
            compositionHost = compositionHost ?? CreatePluginHost(typeof(CodeCheckService).GetTypeInfo().Assembly);

            var workspace = compositionHost.GetExport<OmnisharpWorkspace>();
            AddProjectToWorkspace(workspace, "project.json", new[] { "dnx451", "dnxcore50" }, sourceFiles);

            // Logic is copied from TestHelper, no idea what's waiting for.
            Thread.Sleep(50);

            return workspace;
        }

        private CompositionHost CreatePluginHost(IEnumerable<Assembly> assemblies) => CreatePluginHost(assemblies.ToArray());

        private CompositionHost CreatePluginHost(params Assembly[] assemblies)
        {
            var serviceProvider = new TestServiceProvider(LoggerFactory);



            return Startup.ConfigureMef(
                serviceProvider,
                new FakeOmniSharpOptions().Value,
                assemblies);
        }

        private MetadataReference CreateFromType(Type type)
        {
            var assemblyPath = type.GetTypeInfo().Assembly.Location;
            var reference = MetadataReference.CreateFromFile(assemblyPath);

            _logger.LogInformation($"Create {nameof(MetadataReference)} from assembly {assemblyPath} resolved from type {type.Name}.");

            return reference;
        }
    }
}
