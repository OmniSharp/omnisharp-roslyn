using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class RoslynAnalyzerFacts : AbstractTestFixture
    {
        public class TestAnalyzerReference : AnalyzerReference
        {
            public override string FullPath => null;
            public override object Id => Display;
            public override string Display => nameof(TestAnalyzerReference);

            public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language)
            {
                return new DiagnosticAnalyzer[] { new TestDiagnosticAnalyzer() }.ToImmutableArray();
            }

            public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages()
            {
                return new DiagnosticAnalyzer[] { new TestDiagnosticAnalyzer() }.ToImmutableArray();
            }
        }

        [DiagnosticAnalyzer(LanguageNames.CSharp)]
        public class TestDiagnosticAnalyzer : DiagnosticAnalyzer
        {
            private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
                nameof(TestDiagnosticAnalyzer),
                "Testtitle",
                "Type name '{0}' contains lowercase letters",
                "Naming",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true
            );

            public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            {
                get { return ImmutableArray.Create(Rule); }
            }

            public override void Initialize(AnalysisContext context)
            {
                context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
            }

            private static void AnalyzeSymbol(SymbolAnalysisContext context)
            {
                var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
                if (namedTypeSymbol.Name == "_this_is_invalid_test_class_name")
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Rule,
                        namedTypeSymbol.Locations[0],
                        namedTypeSymbol.Name
                    ));
                }
            }
        }

        public RoslynAnalyzerFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        [Fact]
        public async Task When_custom_analyzers_are_executed_then_return_results()
        {
            var testFile = new TestFile("testFile.cs", "class _this_is_invalid_test_class_name { int n = true; }");

            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);

            var testAnalyzerRef = new TestAnalyzerReference();

            TestHelpers.AddProjectToWorkspace(
                SharedOmniSharpTestHost.Workspace,
                "project.csproj",
                new[] { "netcoreapp2.1" },
                new[] { testFile },
                analyzerRefs: new AnalyzerReference []{ testAnalyzerRef }.ToImmutableArray());

            var analyzerService = SharedOmniSharpTestHost.GetExport<RoslynAnalyzerService>();

            // TODO: This is hack, requires real wait for result routine.
            await Task.Delay(5000);

            Assert.Single(
                analyzerService.GetCurrentDiagnosticResults().Where(x => x.Id == nameof(TestDiagnosticAnalyzer)));
        }

        [Fact]
        public async Task When_custom_analyzer_doesnt_have_match_then_dont_return_it()
        {
            var testFile = new TestFile("testFile.cs", "class SomeClass { int n = true; }");

            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);

            var testAnalyzerRef = new TestAnalyzerReference();

            TestHelpers.AddProjectToWorkspace(
                SharedOmniSharpTestHost.Workspace,
                "project.csproj",
                new[] { "netcoreapp2.1" },
                new[] { testFile },
                analyzerRefs: new AnalyzerReference[] { testAnalyzerRef }.ToImmutableArray());

            var analyzerService = SharedOmniSharpTestHost.GetExport<RoslynAnalyzerService>();

            // TODO: This is hack, requires real wait for result routine.
            await Task.Delay(5000);

            Assert.Empty(
                analyzerService.GetCurrentDiagnosticResults().Where(x => x.Id == nameof(TestDiagnosticAnalyzer)));
        }

        [Fact]
        public async Task Always_return_results_from_net_default_analyzers()
        {
            var testFile = new TestFile("testFile.cs", "class SomeClass { int n = true; }");

            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);

            TestHelpers.AddProjectToWorkspace(
                SharedOmniSharpTestHost.Workspace,
                "project.csproj",
                new[] { "netcoreapp2.1" },
                new[] { testFile });

            var analyzerService = SharedOmniSharpTestHost.GetExport<RoslynAnalyzerService>();

            // TODO: This is hack, requires real wait for result routine.
            await Task.Delay(5000);

            Assert.Empty(
                analyzerService.GetCurrentDiagnosticResults().Where(x => x.Id == "CS5001"));
        }
    }
}
