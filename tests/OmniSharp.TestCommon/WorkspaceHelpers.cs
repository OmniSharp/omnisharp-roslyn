using System;
using System.Collections.Generic;
using System.Composition.Hosting;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Services;

namespace OmniSharp.TestCommon
{
    public static class WorkspaceHelpers
    {
        public static OmnisharpWorkspace CreateCsxWorkspace(string source, string fileName = "dummy.csx")
        {
            var versionStamp = VersionStamp.Create();
            var mscorlib = MetadataReference.CreateFromFile(AssemblyHelpers.GetAssemblyLocationFromType(typeof(object)));
            var systemCore = MetadataReference.CreateFromFile(AssemblyHelpers.GetAssemblyLocationFromType(typeof(Enumerable)));
            var references = new[] { mscorlib, systemCore };
            var workspace = new OmnisharpWorkspace(new HostServicesBuilder(Enumerable.Empty<ICodeActionProvider>()));

            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp6, DocumentationMode.Parse, SourceCodeKind.Script);

            var projectId = ProjectId.CreateNewId(Guid.NewGuid().ToString());
            var project = ProjectInfo.Create(projectId, VersionStamp.Create(), fileName, $"{fileName}.dll", LanguageNames.CSharp, fileName,
                       compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary), metadataReferences: references, parseOptions: parseOptions,
                       isSubmission: true);

            workspace.AddProject(project);
            var document = DocumentInfo.Create(DocumentId.CreateNewId(project.Id), fileName, null, SourceCodeKind.Script, null, fileName)
                .WithSourceCodeKind(SourceCodeKind.Script)
                .WithTextLoader(TextLoader.From(TextAndVersion.Create(SourceText.From(source), VersionStamp.Create())));

            workspace.AddDocument(document);
            return workspace;
        }

        public static OmnisharpWorkspace CreateSimpleWorkspace(string source, string fileName = "dummy.cs")
        {
            return CreateSimpleWorkspace(
                PluginHostHelpers.CreatePluginHost(),
                new Dictionary<string, string> { { fileName, source } });
        }

        public static OmnisharpWorkspace CreateSimpleWorkspace(CompositionHost host, string source, string fileName = "dummy.cs")
        {
            return CreateSimpleWorkspace(host, new Dictionary<string, string> { { fileName, source } });
        }

        public static OmnisharpWorkspace CreateSimpleWorkspace(Dictionary<string, string> sourceFiles)
        {
            return CreateSimpleWorkspace(
                PluginHostHelpers.CreatePluginHost(),
                sourceFiles);
        }

        public static OmnisharpWorkspace CreateSimpleWorkspace(CompositionHost _host, Dictionary<string, string> sourceFiles)
        {
            var host = _host ?? PluginHostHelpers.CreatePluginHost(typeof(CodeCheckService).GetTypeInfo().Assembly);
            var workspace = host.GetExport<OmnisharpWorkspace>();
            workspace.AddProject("project.json", new[] { "dnx451", "dnxcore50" }, sourceFiles);

            Thread.Sleep(50);
            return workspace;
        }

        public static OmnisharpWorkspace AddProject(
            this OmnisharpWorkspace workspace,
            string filePath,
            string[] frameworks,
            Dictionary<string, string> sourceFiles)
        {
            var versionStamp = VersionStamp.Create();

            var mscorlib = MetadataReference.CreateFromFile(AssemblyHelpers.GetAssemblyLocationFromType(typeof(object)));
            var systemCore = MetadataReference.CreateFromFile(AssemblyHelpers.GetAssemblyLocationFromType(typeof(Enumerable)));

            var references = new[] { mscorlib, systemCore };

            foreach (var framework in frameworks)
            {
                var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(),
                                                     versionStamp,
                                                     "OmniSharp+" + framework,
                                                     "AssemblyName",
                                                     LanguageNames.CSharp,
                                                     filePath,
                                                     metadataReferences: references);

                workspace.AddProject(projectInfo);
                foreach (var file in sourceFiles)
                {
                    var document = DocumentInfo.Create(DocumentId.CreateNewId(projectInfo.Id), file.Key,
                                                       null, SourceCodeKind.Regular,
                                                       TextLoader.From(TextAndVersion.Create(SourceText.From(file.Value), versionStamp)), file.Key);

                    workspace.AddDocument(document);
                }
            }

            return workspace;
        }
    }
}
