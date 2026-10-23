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
    public class AnalyzerConfigurationTests : ProjectWithAnalyzersTestBase
    {
        public AnalyzerConfigurationTests(ITestOutputHelper output)
            : base(output)
        {
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
        public async Task WhenProjectIsLoadedThenItContainsAnalyzerConfigurationFromParentFolder()
        {
            using (var testProject = await TestAssets.Instance.GetTestProjectAsync("ProjectWithParentEditorConfig"))
            using (var host = CreateMSBuildTestHost(testProject.Directory, configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true, editorConfigEnabled: true)))
            {
                var project = host.Workspace.CurrentSolution.Projects.Single();
                var projectFolderPath = Path.GetDirectoryName(project.FilePath);
                var projectParentFolderPath = Path.GetDirectoryName(projectFolderPath);

                var analyzerConfigDocument = project.AnalyzerConfigDocuments.Where(document => document.Name.Equals(".editorconfig")).Single();
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
    }
}
