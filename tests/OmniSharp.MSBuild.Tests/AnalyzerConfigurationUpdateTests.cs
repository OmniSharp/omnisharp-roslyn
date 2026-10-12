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
using OmniSharp.Options;
using OmniSharp.Services;
using TestUtility;
using Xunit;

namespace OmniSharp.MSBuild.Tests
{
    public class AnalyzerConfigurationUpdateTests : ProjectWithAnalyzersTestBase
    {
        public AnalyzerConfigurationUpdateTests(ITestOutputHelper output)
            : base(output)
        {
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
                var analyzerConfigDocument = initialProject.AnalyzerConfigDocuments.Where(document => document.Name.Equals(".editorconfig")).Single();

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
                var analyzerConfigDocument = initialProject.AnalyzerConfigDocuments.Where(document => document.Name.Equals(".editorconfig")).Single();

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
    }
}
