using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using OmniSharp;
using OmniSharp.Script;
using OmniSharp.Services;

namespace TestUtility
{
    public static class TestHelpers
    {
        public static OmniSharpWorkspace CreateCsxWorkspace(TestFile testFile)
        {
            var workspace = new OmniSharpWorkspace(new HostServicesAggregator(Enumerable.Empty<IHostServicesProvider>()));
            AddCsxProjectToWorkspace(workspace, testFile);
            return workspace;
        }

        public static void AddCsxProjectToWorkspace(OmniSharpWorkspace workspace, TestFile testFile)
        {
            var references = GetReferences();
            var scriptHelper = new ScriptHelper();            
            var project = scriptHelper.CreateProject(testFile.FileName, references.Union(new[] { MetadataReference.CreateFromFile(typeof(CommandLineScriptGlobals).GetTypeInfo().Assembly.Location) }), Enumerable.Empty<string>());
            workspace.AddProject(project);

            var documentInfo = DocumentInfo.Create(
                id: DocumentId.CreateNewId(project.Id),
                name: testFile.FileName,
                sourceCodeKind: SourceCodeKind.Script,
                loader: TextLoader.From(TextAndVersion.Create(testFile.Content.Text, VersionStamp.Create())),
                filePath: testFile.FileName);
            workspace.AddDocument(documentInfo);
        }

        public static void AddProjectToWorkspace(OmniSharpWorkspace workspace, string filePath, string[] frameworks, TestFile[] testFiles)
        {
            var versionStamp = VersionStamp.Create();
            var references = GetReferences();
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
        }

        private static IEnumerable<PortableExecutableReference> GetReferences()
        {
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

            return references;
        }
    }
}
