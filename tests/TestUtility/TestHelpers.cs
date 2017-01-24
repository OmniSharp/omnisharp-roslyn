using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OmniSharp;
using OmniSharp.Services;

namespace TestUtility
{
    public static class TestHelpers
    {
        public static OmnisharpWorkspace CreateCsxWorkspace(TestFile testFile)
        {
            var versionStamp = VersionStamp.Create();
            var mscorlib = MetadataReference.CreateFromFile(AssemblyHelpers.FromType(typeof(object)).Location);
            var systemCore = MetadataReference.CreateFromFile(AssemblyHelpers.FromType(typeof(Enumerable)).Location);
            var references = new[] { mscorlib, systemCore };
            var workspace = new OmnisharpWorkspace(
                new HostServicesAggregator(
                    Enumerable.Empty<IHostServicesProvider>()));

            var parseOptions = new CSharpParseOptions(
                LanguageVersion.Default,
                DocumentationMode.Parse,
                SourceCodeKind.Script);

            var project = ProjectInfo.Create(
                id: ProjectId.CreateNewId(),
                version: VersionStamp.Create(),
                name: testFile.FileName,
                assemblyName: $"{testFile.FileName}.dll",
                language: LanguageNames.CSharp,
                filePath: testFile.FileName,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                metadataReferences: references,
                parseOptions: parseOptions,
                isSubmission: true);

            workspace.AddProject(project);
            var documentInfo = DocumentInfo.Create(
                id: DocumentId.CreateNewId(project.Id),
                name: testFile.FileName,
                sourceCodeKind: SourceCodeKind.Script,
                loader: TextLoader.From(TextAndVersion.Create(testFile.Content.Text, VersionStamp.Create())),
                filePath: testFile.FileName);

            workspace.AddDocument(documentInfo);
            return workspace;
        }

        public static Task AddProjectToWorkspaceAsync(OmnisharpWorkspace workspace, string filePath, string[] frameworks, TestFile[] testFiles)
        {
            var versionStamp = VersionStamp.Create();

            // This is a bit messy. Essentially, we need to add all assemblies that type forwarders might point to.

            var assemblies = new[]
            {
                AssemblyHelpers.FromType(typeof(object)),
                AssemblyHelpers.FromType(typeof(Enumerable)),
                AssemblyHelpers.FromType(typeof(Stack<>)),
                AssemblyHelpers.FromType(typeof(Lazy<,>)),
                AssemblyHelpers.FromName("System.Runtime"),
                AssemblyHelpers.FromName("mscorlib")
            };

            var references = assemblies
                .Where(a => a != null)
                .Select(a => a.Location)
                .Distinct()
                .Select(l => MetadataReference.CreateFromFile(l));

            frameworks = frameworks ?? new[] { string.Empty };

            foreach (var framework in frameworks)
            {
                var projectInfo = ProjectInfo.Create(
                    id: ProjectId.CreateNewId(),
                    version: versionStamp,
                    name: "OmniSharp+" + framework,
                    assemblyName: "AssemblyName",
                    language: LanguageNames.CSharp,
                    filePath: filePath,
                    metadataReferences: references);

                workspace.AddProject(projectInfo);

                foreach (var testFile in testFiles)
                {
                    var documentInfo = DocumentInfo.Create(
                        id: DocumentId.CreateNewId(projectInfo.Id),
                        name: testFile.FileName,
                        sourceCodeKind: SourceCodeKind.Regular,
                        loader: TextLoader.From(TextAndVersion.Create(testFile.Content.Text, versionStamp)),
                        filePath: testFile.FileName);

                    workspace.AddDocument(documentInfo);
                }
            }

            return Task.FromResult(workspace);
        }
    }
}
