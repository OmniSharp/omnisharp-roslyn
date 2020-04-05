using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using OmniSharp.FileWatching;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Models.Events;
using OmniSharp.Models.FilesChanged;
using OmniSharp.Models.ProjectInformation;
using OmniSharp.Roslyn.CSharp.Workers.Diagnostics;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

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
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers");
            using var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true));

            await host.RestoreProject(testProject);

            var diagnostics = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "Program.cs"));

            Assert.NotEmpty(diagnostics.QuickFixes);
            Assert.Contains(diagnostics.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "IDE0060"); // Unused args, if restore doesn't rigger re-analysis there is errors like missing System, not this one.
        }

        [Fact]
        public async Task WhenProjectHasAnalyzersItDoesntLockAnalyzerDlls()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers");
            using var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true));

            await host.RestoreProject(testProject);

            var analyzerReferences = host.Workspace.CurrentSolution.Projects.Single().AnalyzerReferences.ToList();

            Assert.NotEmpty(analyzerReferences);

            // This should not throw when analyzers are shadow copied.
            Directory.Delete(Path.Combine(testProject.Directory, "./nugets"), true);
        }

        [Fact]
        public async Task WhenProjectIsLoadedThenItContainsCustomRulesetsFromCsproj()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers");
            using var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true));

            var project = host.Workspace.CurrentSolution.Projects.Single();

            Assert.Contains(project.CompilationOptions.SpecificDiagnosticOptions, x => x.Key == "CA1021" && x.Value == ReportDiagnostic.Warn);
        }

        [Theory]
        [InlineData("ProjectWithDisabledAnalyzers")]
        [InlineData("ProjectWithDisabledAnalyzers2")]
        public async Task WhenProjectWithRunAnalyzersDisabledIsLoadedThenAnalyzersAreIgnored(string projectName)
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync(projectName);
            using var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true));

            await host.RestoreProject(testProject);

            var analyzerReferences = host.Workspace.CurrentSolution.Projects.Single().AnalyzerReferences.ToList();

            Assert.Empty(analyzerReferences);
        }

        [Fact]
        public async Task WhenProjectRulesetFileIsChangedThenUpdateRulesAccordingly()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers");
            using var host = CreateMSBuildTestHost(testProject.Directory);

            var csprojFile = ModifyXmlFileInPlace(Path.Combine(testProject.Directory, "ProjectWithAnalyzers.csproj"),
                csprojFileXml => csprojFileXml.Descendants("CodeAnalysisRuleSet").Single().Value = "witherrorlevel.ruleset");

            var emitter = host.GetTestEventEmitter();
            emitter.Clear();

            await NotifyFileChanged(host, csprojFile);

            await emitter.WaitForMessage<ProjectInformationResponse>();

            var project = host.Workspace.CurrentSolution.Projects.Single();
            Assert.Contains(project.CompilationOptions.SpecificDiagnosticOptions, x => x.Key == "CA1021" && x.Value == ReportDiagnostic.Error);
        }

        [Fact]
        public async Task WhenProjectRulesetFileRuleIsUpdatedThenUpdateRulesAccordingly()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers");
            using var host = CreateMSBuildTestHost(testProject.Directory);

            var ruleFile = ModifyXmlFileInPlace(Path.Combine(testProject.Directory, "default.ruleset"),
                ruleXml => ruleXml.Descendants("Rule").Single().Attribute("Action").Value = "Error");

            var emitter = host.GetTestEventEmitter();
            emitter.Clear();

            await NotifyFileChanged(host, ruleFile);

            await emitter.WaitForMessage<ProjectInformationResponse>();

            var project = host.Workspace.CurrentSolution.Projects.Single();
            Assert.Contains(project.CompilationOptions.SpecificDiagnosticOptions, x => x.Key == "CA1021" && x.Value == ReportDiagnostic.Error);
        }

        // Unstable with MSBuild 16.3 on *nix
        [ConditionalFact(typeof(WindowsOnly))]
        public async Task WhenNewAnalyzerReferenceIsAdded_ThenAutomaticallyUseItWithoutRestart()
        {
            using var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithAnalyzers");
            using var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true));

            var csprojFile = ModifyXmlFileInPlace(Path.Combine(testProject.Directory, "ProjectWithAnalyzers.csproj"),
                csprojFileXml =>
                {
                    var referencesGroup = csprojFileXml.Descendants("ItemGroup").First();
                    referencesGroup.Add(new XElement("PackageReference", new XAttribute("Include", "Roslynator.Analyzers"), new XAttribute("Version", "2.1.0")));
                });

            var emitter = host.GetTestEventEmitter();

            await NotifyFileChanged(host, csprojFile);
            await emitter.WaitForMessage<ProjectDiagnosticStatusMessage>(x => x.Status == ProjectDiagnosticStatus.Ready);
            emitter.Clear();

            await host.RestoreProject(testProject);

            await emitter.WaitForMessage<ProjectDiagnosticStatusMessage>(x => x.Status == ProjectDiagnosticStatus.Ready);

            var diagnostics = await host.RequestCodeCheckAsync(Path.Combine(testProject.Directory, "Program.cs"));
            Assert.Contains(diagnostics.QuickFixes.OfType<DiagnosticLocation>(), x => x.Id == "RCS1102"); // Analysis result from roslynator.
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
    }
}
