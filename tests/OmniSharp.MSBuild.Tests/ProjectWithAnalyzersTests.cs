using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Eventing;
using OmniSharp.FileWatching;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.FilesChanged;
using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;
using static OmniSharp.MSBuild.Tests.ProjectLoadListenerTests;

namespace OmniSharp.MSBuild.Tests
{
    public class ProjectWithAnalyzersTests : AbstractMSBuildTestFixture
    {
        public ProjectWithAnalyzersTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task WhenProjectIsRestoredThenReanalyzeProject()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true)))
            {
                await host.RestoreProject(testProject);

                await Task.Delay(2000);

                var diagnostics = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "Program.cs"));

                Assert.NotEmpty(diagnostics.QuickFixes);
                Assert.Contains(diagnostics.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "IDE0060"); // Unused args.
            }
        }

        [Fact]
        public async Task WhenProjectHasAnalyzersItDoesntLockAnalyzerDlls()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers"))
            {
                // TODO: Restore when host is running doesn't reload new analyzer references yet, move this
                // after host start after that is fixed.
                await RestoreProject(testProject);

                using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true)))
                {
                    var analyzerReferences = host.Workspace.CurrentSolution.Projects.Single().AnalyzerReferences.ToList();

                    Assert.NotEmpty(analyzerReferences);

                    // This should not throw when analyzers are shadow copied.
                    Directory.Delete(Path.Combine(testProject.Directory, "./nugets"), true);
                }
            }
        }

        [Fact]
        public async Task WhenProjectIsLoadedThenItContainsCustomRulesetsFromCsproj()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true)))
            {
                var project = host.Workspace.CurrentSolution.Projects.Single();

                Assert.Contains(project.CompilationOptions.SpecificDiagnosticOptions, x => x.Key == "CA1021" && x.Value == ReportDiagnostic.Warn);
            }
        }

        [Fact]
        public async Task WhenProjectIsLoadedThenItContainsAnalyzerConfigurationFromEditorConfig()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzersAndEditorConfig"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true, editorConfigEnabled: true)))
            {
                var diagnostics = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "Program.cs"));

                Assert.NotEmpty(diagnostics.QuickFixes);

                var quickFix = diagnostics.QuickFixes.OfType<DiagnosticLocation>().Single(x => x.Id == "IDE0005");
                Assert.Equal("Error", quickFix.LogLevel);
            }
        }

        [Fact]
        public async Task WhenProjectEditorConfigIsChangedThenAnalyzerConfigurationUpdates()
        {
            var emitter = new ProjectLoadTestEventEmitter();

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzersAndEditorConfig"))
            using (var host = CreateMSBuildTestHost(
                testProject.Directory,
                emitter.AsExportDescriptionProvider(LoggerFactory),
                TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true, editorConfigEnabled: true)))
            {
                var initialProject = host.Workspace.CurrentSolution.Projects.Single();
                var analyzerConfigDocument = initialProject.AnalyzerConfigDocuments.Single();

                File.WriteAllText(analyzerConfigDocument.FilePath, @"
root = true

[*.cs]
# IDE0005: Unnecessary using
dotnet_diagnostic.IDE0005.severity = none
");

                await NotifyFileChanged(host, analyzerConfigDocument.FilePath);

                emitter.WaitForProjectUpdate();

                var diagnostics = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "Program.cs"));

                Assert.NotEmpty(diagnostics.QuickFixes);
                Assert.DoesNotContain(diagnostics.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "IDE0005");
            }
        }

        [Fact]
        public async Task WhenProjectChangesAnalyzerConfigurationIsPreserved()
        {
            var emitter = new ProjectLoadTestEventEmitter();

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzersAndEditorConfig"))
            using (var host = CreateMSBuildTestHost(
                testProject.Directory,
                emitter.AsExportDescriptionProvider(LoggerFactory),
                TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true, editorConfigEnabled: true)))
            {
                var initialProject = host.Workspace.CurrentSolution.Projects.Single();
                var firstDiagnosticsSet = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "Program.cs"));
                Assert.NotEmpty(firstDiagnosticsSet.QuickFixes);
                Assert.Contains(firstDiagnosticsSet.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "IDE0005" && x.LogLevel == "Error");

                // report reloading of a project
                await NotifyFileChanged(host, initialProject.FilePath);
                emitter.WaitForProjectUpdate();

                var secondDiagnosticsSet = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "Program.cs"));
                Assert.NotEmpty(secondDiagnosticsSet.QuickFixes);
                Assert.Contains(secondDiagnosticsSet.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "IDE0005" && x.LogLevel == "Error");
            }
        }

        [Fact]
        public async Task WhenProjectIsLoadedThenItContainsAnalyzerConfigurationFromParentFolder()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithParentEditorConfig"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true, editorConfigEnabled: true)))
            {
                var project = host.Workspace.CurrentSolution.Projects.Single();
                var projectFolderPath = Path.GetDirectoryName(project.FilePath);
                var projectParentFolderPath = Path.GetDirectoryName(projectFolderPath);

                var analyzerConfigDocument = project.AnalyzerConfigDocuments.Single();
                var editorConfigFolderPath = Path.GetDirectoryName(analyzerConfigDocument.FilePath);

                Assert.Equal(projectParentFolderPath, editorConfigFolderPath);
            }
        }

        [Fact]
        public async Task WhenProjectIsLoadedThenItContainsAnalyzerConfigurationFromParentEditorConfig()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithParentEditorConfig"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true, editorConfigEnabled: true)))
            {
                var project = host.Workspace.CurrentSolution.Projects.Single();
                var projectFolderPath = Path.GetDirectoryName(project.FilePath);

                var diagnostics = await host.RequestCodeCheckAsync(Path.Combine(projectFolderPath, "Program.cs"));

                Assert.NotEmpty(diagnostics.QuickFixes);

                var quickFix = diagnostics.QuickFixes.OfType<DiagnosticLocation>().Single(x => x.Id == "IDE0005");
                Assert.Equal("Error", quickFix.LogLevel);
            }
        }

        [Fact]
        public async Task WhenProjectParentEditorConfigIsChangedThenAnalyzerConfigurationUpdates()
        {
            var emitter = new ProjectLoadTestEventEmitter();

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithParentEditorConfig"))
            using (var host = CreateMSBuildTestHost(
                testProject.Directory,
                emitter.AsExportDescriptionProvider(LoggerFactory),
                TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true, editorConfigEnabled: true)))
            {
                var initialProject = host.Workspace.CurrentSolution.Projects.Single();
                var analyzerConfigDocument = initialProject.AnalyzerConfigDocuments.Single();

                File.WriteAllText(analyzerConfigDocument.FilePath, @"
root = true

[*.cs]
# IDE0005: Unnecessary using
dotnet_diagnostic.IDE0005.severity = none
");

                await NotifyFileChanged(host, analyzerConfigDocument.FilePath);

                emitter.WaitForProjectUpdate();

                var project = host.Workspace.CurrentSolution.Projects.Single();
                var projectFolderPath = Path.GetDirectoryName(project.FilePath);
                var diagnostics = await host.RequestCodeCheckAsync(Path.Combine(projectFolderPath, "Program.cs"));

                Assert.NotEmpty(diagnostics.QuickFixes);
                Assert.DoesNotContain(diagnostics.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "IDE0005");
            }
        }

        [Theory]
        [InlineData("ProjectWithDisabledAnalyzers")]
        [InlineData("ProjectWithDisabledAnalyzers2")]
        public async Task WhenProjectWithRunAnalyzersDisabledIsLoadedThenAnalyzersAreIgnored(string projectName)
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName);
            await RestoreProject(testProject);

            using var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true));
            var analyzerReferences = host.Workspace.CurrentSolution.Projects.Single().AnalyzerReferences.ToList();

            Assert.Empty(analyzerReferences);
        }

        [Fact]
        public async Task WhenProjectRulesetFileIsChangedThenUpdateRulesAccordingly()
        {
            var emitter = new ProjectLoadTestEventEmitter();

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory)))
            {
                var csprojFile = ModifyXmlFileInPlace(Path.Combine(testProject.Directory, "ProjectWithAnalyzers.csproj"),
                    csprojFileXml => csprojFileXml.Descendants("CodeAnalysisRuleSet").Single().Value = "witherrorlevel.ruleset");

                await NotifyFileChanged(host, csprojFile);

                emitter.WaitForProjectUpdate();

                var project = host.Workspace.CurrentSolution.Projects.Single();
                Assert.Contains(project.CompilationOptions.SpecificDiagnosticOptions, x => x.Key == "CA1021" && x.Value == ReportDiagnostic.Error);
            }
        }

        [Fact]
        public async Task WhenProjectRulesetFileRuleIsUpdatedThenUpdateRulesAccordingly()
        {
            var emitter = new ProjectLoadTestEventEmitter();

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory)))
            {
                var ruleFile = ModifyXmlFileInPlace(Path.Combine(testProject.Directory, "default.ruleset"),
                    ruleXml => ruleXml.Descendants("Rule").Single().Attribute("Action").Value = "Error");

                await NotifyFileChanged(host, ruleFile);

                emitter.WaitForProjectUpdate();

                var project = host.Workspace.CurrentSolution.Projects.Single();
                Assert.Contains(project.CompilationOptions.SpecificDiagnosticOptions, x => x.Key == "CA1021" && x.Value == ReportDiagnostic.Error);
            }
        }

        // Unstable with MSBuild 16.3 on *nix
        [ConditionalFact(typeof(WindowsOnly))]
        public async Task WhenNewAnalyzerReferenceIsAdded_ThenAutomaticallyUseItWithoutRestart()
        {
            var emitter = new ProjectLoadTestEventEmitter();

            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, emitter.AsExportDescriptionProvider(LoggerFactory), configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true)))
            {
                var csprojFile = ModifyXmlFileInPlace(Path.Combine(testProject.Directory, "ProjectWithAnalyzers.csproj"),
                    csprojFileXml =>
                    {
                        var referencesGroup = csprojFileXml.Descendants("ItemGroup").FirstOrDefault();
                        referencesGroup.Add(new XElement("PackageReference", new XAttribute("Include", "Roslynator.Analyzers"), new XAttribute("Version", "2.1.0")));
                    });

                await NotifyFileChanged(host, csprojFile);

                emitter.WaitForProjectUpdate();
                await host.RestoreProject(testProject);

                // Todo: This can be removed and replaced with wait for event (project analyzed eg.) once they are available.
                await Task.Delay(2000);

                var diagnostics = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "Program.cs"));

                Assert.Contains(diagnostics.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "RCS1102"); // Analysis result from roslynator.
            }
        }

        [Fact]
        public async Task WhenProjectIsLoadedThenItRespectsDiagnosticSuppressors()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("TwoProjectsWithAnalyzerSuppressor"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true)))
            {
                var project = host.Workspace.CurrentSolution.Projects.First(p => p.Name == "App");

                // by default Stylecop reported diagnostics should be:
                //  - The file header is missing or not located at the top of the file. [App] SA1633
                //  - Elements should be documented [App] SA1600
                //  - Element 'Program' should declare an access modifier [App] SA1400
                //  - Element 'Main' should declare an access modifier [App] SA1400
                // However, SA1200 should be suppressed

                var diagnostics = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "App", "Program.cs"));

                Assert.NotEmpty(diagnostics.QuickFixes);

                Assert.Contains(diagnostics.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "SA1633" && x.LogLevel == "Warning");
                Assert.Contains(diagnostics.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "SA1600" && x.LogLevel == "Warning");
                Assert.Contains(diagnostics.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "SA1400" && x.LogLevel == "Warning");
                Assert.DoesNotContain(diagnostics.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "SA1200");
            }
        }

        private string ModifyXmlFileInPlace(string file, Action<XDocument> docUpdateAction)
        {
            var xmlFile = XDocument.Load(file);
            docUpdateAction(xmlFile);
            xmlFile.Save(file);
            return file;
        }

        private static async Task NotifyFileChanged(OmniSharpTestHost host, string file)
        {
            await host.GetFilesChangedService().Handle(new[] {
                    new FilesChangedRequest() {
                    FileName = file,
                    ChangeType = FileChangeType.Change
                    }
                });
        }

        private static async Task RestoreProject(ITestProject testProject)
        {
            await new DotNetCliService(new LoggerFactory(), NullEventEmitter.Instance).RestoreAsync(testProject.Directory);
        }
    }
}
