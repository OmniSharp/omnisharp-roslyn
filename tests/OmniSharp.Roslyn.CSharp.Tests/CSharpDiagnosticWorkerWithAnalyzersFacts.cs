using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using OmniSharp.Services;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class CSharpDiagnosticWorkerWithAnalyzersFacts : AbstractTestFixture
    {
        private const string TestFilename = "dummy.cs";
        private const string TestFilename2 = "dummy2.cs";
        private const string TestCode = @"
public class Program
{
    public static void Main(string[] args)
    {
    }
}";

        public CSharpDiagnosticWorkerWithAnalyzersFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        [Fact]
        public async Task ProjectAnalyzers_NotFiltered_WhenSeverityIsDefault()
        {
            var editorconfigFile = CreateFile(".editorconfig", "root = true");
            var testFile = CreateFile(TestFilename, TestCode);

            var filteredAnalyzers = await GetFilteredProjectAnalyzersAsync(testFile, editorconfigFile);

            Assert.NotEmpty(filteredAnalyzers);
        }

        [Fact]
        public async Task ProjectAnalyzers_AllFiltered_WhenAnalyzerDiagnosticSeverityNone()
        {
            var editorconfigFile = CreateFile(".editorconfig", @"
root = true

[*.cs]
# Disable all diagnostics
dotnet_analyzer_diagnostic.severity = none
");
            var testFile = CreateFile(TestFilename, TestCode);

            var filteredAnalyzers = await GetFilteredProjectAnalyzersAsync(testFile, editorconfigFile);

            Assert.Empty(filteredAnalyzers);
        }

        [Fact]
        public async Task ProjectAnalyzers_SingleAnalyzer_WhenSeverityIsSet()
        {
            var editorconfigFile = CreateFile(".editorconfig", @"
root = true

[*.cs]
# Disable all diagnostics
dotnet_analyzer_diagnostic.severity = none

# Enable a single diagnostic
dotnet_diagnostic.IDE0055.severity = error
");
            var testFile = CreateFile(TestFilename, TestCode);

            var filteredAnalyzers = await GetFilteredProjectAnalyzersAsync(testFile, editorconfigFile);

            Assert.Single(filteredAnalyzers);
            Assert.Equal("IDE0055", filteredAnalyzers[0].SupportedDiagnostics[0].Id);
        }

        [Fact]
        public async Task DocumentAnalyzers_NotFiltered_WhenSeverityIsDefault()
        {
            var editorconfigFile = CreateFile(".editorconfig", "root = true");
            var testFile = CreateFile(TestFilename, TestCode);
            var testFile2 = CreateFile(TestFilename2, TestCode);

            var filteredAnalyzerMap = await GetFilteredDocumentAnalyzersAsync(testFile, testFile2, editorconfigFile);

            var testFileAnalyzers = filteredAnalyzerMap[testFile];
            Assert.NotEmpty(testFileAnalyzers);

            var testFile2Analyzers = filteredAnalyzerMap[testFile2];
            Assert.NotEmpty(testFile2Analyzers);

            Assert.Equal(testFileAnalyzers.Length, testFile2Analyzers.Length);
        }

        [Fact]
        public async Task DocumentAnalyzers_AllFiltered_WhenAnalyzerDiagnosticSeverityNone()
        {
            var editorconfigFile = CreateFile(".editorconfig", @"
root = true

[*.cs]
# Disable all diagnostics
dotnet_analyzer_diagnostic.severity = none
");
            var testFile = CreateFile(TestFilename, TestCode);
            var testFile2 = CreateFile(TestFilename2, TestCode);

            var filteredAnalyzerMap = await GetFilteredDocumentAnalyzersAsync(testFile, testFile2, editorconfigFile);

            var testFileAnalyzers = filteredAnalyzerMap[testFile];
            Assert.Empty(testFileAnalyzers);

            var testFile2Analyzers = filteredAnalyzerMap[testFile2];
            Assert.Empty(testFile2Analyzers);
        }

        [Fact]
        public async Task DocumentAnalyzers_SingleAnalyzer_WhenSeverityIsSet()
        {
            var editorconfigFile = CreateFile(".editorconfig", $@"
root = true

[*.cs]
# Disable all diagnostics
dotnet_analyzer_diagnostic.severity = none

[**\{TestFilename2}]
# Enable a single diagnostic
dotnet_diagnostic.IDE0055.severity = error
");
            var testFile = CreateFile(TestFilename, TestCode);
            var testFile2 = CreateFile(TestFilename2, TestCode);

            var filteredAnalyzerMap = await GetFilteredDocumentAnalyzersAsync(testFile, testFile2, editorconfigFile);

            var testFileAnalyzers = filteredAnalyzerMap[testFile];
            Assert.Empty(testFileAnalyzers);

            var testFile2Analyzers = filteredAnalyzerMap[testFile2];
            Assert.Single(testFile2Analyzers);
            Assert.Equal("IDE0055", testFile2Analyzers[0].SupportedDiagnostics[0].Id);
        }


        private async Task<ImmutableArray<DiagnosticAnalyzer>> GetFilteredProjectAnalyzersAsync(TestFile codeFile, TestFile editorconfigFile)
        {
            using var host = CreateOmniSharpHost(new[] { codeFile, editorconfigFile });

            var project = host.Workspace.CurrentSolution.Projects.Single();
            var compilation = await project.GetCompilationAsync();

            var allAnalyzers = GetConfigurableAnalyzers(host, project);

            Assert.NotEmpty(allAnalyzers);

            return await CSharpDiagnosticWorkerWithAnalyzers.FilterAnalyzersBySeverityAsync(allAnalyzers, project, compilation, ReportDiagnostic.Hidden);
        }

        private async Task<ImmutableDictionary<TestFile, ImmutableArray<DiagnosticAnalyzer>>> GetFilteredDocumentAnalyzersAsync(TestFile codeFile, TestFile codeFile2, TestFile editorconfigFile)
        {
            using var host = CreateOmniSharpHost(new[] { codeFile, codeFile2, editorconfigFile });

            var project = host.Workspace.CurrentSolution.Projects.Single();
            var compilation = await project.GetCompilationAsync();

            var allAnalyzers = GetConfigurableAnalyzers(host, project);

            Assert.NotEmpty(allAnalyzers);

            var analyzersMap = ImmutableDictionary.CreateBuilder<TestFile, ImmutableArray<DiagnosticAnalyzer>>();

            foreach (var document in project.Documents)
            {
                var documentAnalyzers = await CSharpDiagnosticWorkerWithAnalyzers.FilterAnalyzersBySeverityAsync(allAnalyzers, document, project.AnalyzerOptions, compilation, ReportDiagnostic.Hidden);

                var testFile = document.FilePath == codeFile.FileName
                    ? codeFile
                    : codeFile2;
                analyzersMap.Add(testFile, documentAnalyzers);
            }

            Assert.Equal(2, analyzersMap.Count);

            return analyzersMap.ToImmutable();
        }

        private static TestFile CreateFile(string filename, string contents)
            => new(Path.Combine(TestAssets.Instance.TestProjectsFolder, filename), contents);

        private static ImmutableArray<DiagnosticAnalyzer> GetConfigurableAnalyzers(OmniSharpTestHost host, Project project)
        {
            var providers = host.CompositionHost.GetExports<ICodeActionProvider>().ToImmutableArray();
            return providers
                .SelectMany(x => x.CodeDiagnosticAnalyzerProviders)
                .Concat(project.AnalyzerReferences.SelectMany(x => x.GetAnalyzers(project.Language)))
                .Where(diagnostic => diagnostic is not DiagnosticSuppressor
                    && !diagnostic.SupportedDiagnostics.Any(descriptor => descriptor.CustomTags.Contains(WellKnownDiagnosticTags.NotConfigurable)))
                .ToImmutableArray();
        }
    }
}
