using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniSharp;
using OmniSharp.FileWatching;
using OmniSharp.MSBuild.Discovery;
using OmniSharp.Roslyn.EditorConfig;
using OmniSharp.Script;
using OmniSharp.Services;

namespace TestUtility
{
    public static class TestHelpers
    {
        public static OmniSharpWorkspace CreateCsxWorkspace(TestFile testFile)
        {
            var workspace = new OmniSharpWorkspace(new HostServicesAggregator(Enumerable.Empty<IHostServicesProvider>(), new LoggerFactory()), new LoggerFactory(), new ManualFileSystemWatcher());
            AddCsxProjectToWorkspace(workspace, testFile);
            return workspace;
        }

        public static void AddCsxProjectToWorkspace(OmniSharpWorkspace workspace, TestFile testFile)
        {
            var references = GetReferences();
            var scriptHelper = new ScriptProjectProvider(new ScriptOptions(), new OmniSharpEnvironment(), new LoggerFactory(), isDesktopClr: true, editorConfigEnabled: true);
            var project = scriptHelper.CreateProject(testFile.FileName, references.Union(new[] { MetadataReference.CreateFromFile(typeof(CommandLineScriptGlobals).GetTypeInfo().Assembly.Location) }), testFile.FileName, typeof(CommandLineScriptGlobals), Enumerable.Empty<string>());
            workspace.AddProject(project);

            var documentInfo = DocumentInfo.Create(
                id: DocumentId.CreateNewId(project.Id),
                name: testFile.FileName,
                sourceCodeKind: SourceCodeKind.Script,
                loader: TextLoader.From(TextAndVersion.Create(testFile.Content.Text, VersionStamp.Create())),
                filePath: testFile.FileName);
            workspace.AddDocument(documentInfo);
        }

        public static IEnumerable<ProjectId> AddProjectToWorkspace(OmniSharpWorkspace workspace, string filePath, string[] frameworks, TestFile[] testFiles, TestFile[] otherFiles = null, ImmutableArray<AnalyzerReference> analyzerRefs = default)
        {
            otherFiles ??= Array.Empty<TestFile>();

            var versionStamp = VersionStamp.Create();
            var references = GetReferences();
            frameworks = frameworks ?? new[] { string.Empty };
            var projectsIds = new List<ProjectId>();
            var editorConfigPaths = EditorConfigFinder.GetEditorConfigPaths(filePath);

            foreach (var framework in frameworks)
            {
                var projectId = ProjectId.CreateNewId();
                var analyzerConfigDocuments = editorConfigPaths.Select(path =>
                        DocumentInfo.Create(
                            DocumentId.CreateNewId(projectId),
                            name: ".editorconfig",
                            loader: new FileTextLoader(path, Encoding.UTF8),
                            filePath: path))
                    .ToImmutableArray();

                var projectInfo = ProjectInfo.Create(
                    id: projectId,
                    version: versionStamp,
                    name: "OmniSharp+" + framework,
                    assemblyName: "AssemblyName",
                    language: LanguageNames.CSharp,
                    filePath: filePath,
                    metadataReferences: references,
                    analyzerReferences: analyzerRefs)
                    .WithDefaultNamespace("OmniSharpTest")
                    .WithAnalyzerConfigDocuments(analyzerConfigDocuments);

                workspace.AddProject(projectInfo);

                foreach (var testFile in testFiles)
                {
                    workspace.AddDocument(projectInfo.Id, testFile.FileName, TextLoader.From(TextAndVersion.Create(testFile.Content.Text, versionStamp)), SourceCodeKind.Regular);
                }

                foreach (var otherFile in otherFiles)
                {
                    workspace.AddAdditionalDocument(projectInfo.Id, otherFile.FileName, TextLoader.From(TextAndVersion.Create(otherFile.Content.Text, versionStamp)));
                }

                projectsIds.Add(projectInfo.Id);
            }

            return projectsIds;
        }

        private static ImmutableArray<PortableExecutableReference> _references;

        private static IEnumerable<PortableExecutableReference> GetReferences()
        {
            if (!_references.IsDefaultOrEmpty)
            {
                return _references;
            }

            // This is a bit messy. Essentially, we need to add all assemblies that type forwarders might point to.
            var assemblies = new[]
            {
                AssemblyHelpers.FromType(typeof(object)),
                AssemblyHelpers.FromType(typeof(Enumerable)),
                AssemblyHelpers.FromType(typeof(Stack<>)),
                AssemblyHelpers.FromType(typeof(Lazy<,>)),
                AssemblyHelpers.FromName("System.Runtime"),
#if NETCOREAPP
                AssemblyHelpers.FromType(typeof(Console)),
#else
                AssemblyHelpers.FromName("mscorlib")
#endif
            };

            _references = assemblies
                .Where(a => a != null)
                .Select(a => a.Location)
                .Distinct()
                .Select(l => MetadataReference.CreateFromFile(l))
                .ToImmutableArray();

            return _references;
        }

        public static MSBuildInstance AddDotNetCoreToFakeInstance(this MSBuildInstance instance)
        {
            const string dotnetSdkResolver = "Microsoft.DotNet.MSBuildSdkResolver";

            var directory = Path.Combine(
                instance.MSBuildPath,
                "SdkResolvers",
                dotnetSdkResolver
            );

            Directory.CreateDirectory(directory);

            TestIO.TouchFakeFile(Path.Combine(directory, dotnetSdkResolver + ".dll"));

            return instance;
        }

        public static IConfiguration GetConfigurationDataWithAnalyzerConfig(
            bool roslynAnalyzersEnabled = false,
            bool analyzeOpenDocumentsOnly = false,
            bool editorConfigEnabled = false,
            Dictionary<string, string> existingConfiguration = null)
        {
            if (existingConfiguration == null)
            {
                return new Dictionary<string, string>()
                {
                    { "RoslynExtensionsOptions:EnableAnalyzersSupport", roslynAnalyzersEnabled.ToString() },
                    { "RoslynExtensionsOptions:AnalyzeOpenDocumentsOnly", analyzeOpenDocumentsOnly.ToString() },
                    { "FormattingOptions:EnableEditorConfigSupport", editorConfigEnabled.ToString() }
                }.ToConfiguration();
            }

            var copyOfExistingConfigs = existingConfiguration.ToDictionary(x => x.Key, x => x.Value);
            copyOfExistingConfigs.Add("RoslynExtensionsOptions:EnableAnalyzersSupport", roslynAnalyzersEnabled.ToString());
            copyOfExistingConfigs.Add("RoslynExtensionsOptions:AnalyzeOpenDocumentsOnly", analyzeOpenDocumentsOnly.ToString());
            copyOfExistingConfigs.Add("FormattingOptions:EnableEditorConfigSupport", editorConfigEnabled.ToString());

            return copyOfExistingConfigs.ToConfiguration();
        }

        public static async Task WaitUntil(Func<Task<bool>> condition, int frequency = 25, int timeout = -1)
        {
            var waitTask = Task.Run(async () =>
            {
                while (!await condition()) await Task.Delay(frequency);
            });

            if (waitTask != await Task.WhenAny(waitTask,
                    Task.Delay(timeout)))
                throw new TimeoutException();
        }

        public static void SetDefaultCulture()
        {
            CultureInfo ci = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
        }
    }
}
