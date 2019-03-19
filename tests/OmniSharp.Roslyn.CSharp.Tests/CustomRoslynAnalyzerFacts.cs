using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class CustomRoslynAnalyzerFacts
    {
        public class TestAnalyzerReference : AnalyzerReference
        {
            private readonly string _id;
            private readonly bool _isEnabledByDefault;

            public TestAnalyzerReference(string testAnalyzerId, bool isEnabledByDefault = true)
            {
                _id = testAnalyzerId;
                _isEnabledByDefault = isEnabledByDefault;
            }

            public override string FullPath => null;
            public override object Id => _id;
            public override string Display => $"{nameof(TestAnalyzerReference)}_{Id}";

            public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language)
            {
                return new DiagnosticAnalyzer[] { new TestDiagnosticAnalyzer(Id.ToString(), _isEnabledByDefault) }.ToImmutableArray();
            }

            public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages()
            {
                return new DiagnosticAnalyzer[] { new TestDiagnosticAnalyzer(Id.ToString(), _isEnabledByDefault) }.ToImmutableArray();
            }
        }

        [DiagnosticAnalyzer(LanguageNames.CSharp)]
        public class TestDiagnosticAnalyzer : DiagnosticAnalyzer
        {
            public TestDiagnosticAnalyzer(string id, bool isEnabledByDefault)
            {
                this.id = id;
                _isEnabledByDefault = isEnabledByDefault;
            }

            private DiagnosticDescriptor Rule => new DiagnosticDescriptor(
                this.id,
                "Testtitle",
                "Type name '{0}' contains lowercase letters",
                "Naming",
                DiagnosticSeverity.Error,
                isEnabledByDefault: _isEnabledByDefault
            );

            private readonly string id;
            private readonly bool _isEnabledByDefault;

            public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            {
                get { return ImmutableArray.Create(Rule); }
            }

            public override void Initialize(AnalysisContext context)
            {
                context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
            }

            private void AnalyzeSymbol(SymbolAnalysisContext context)
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

        private readonly ITestOutputHelper _testOutput;

        public CustomRoslynAnalyzerFacts(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Fact]
        public async Task When_custom_analyzers_are_executed_then_return_results()
        {
            using (var host = GetHost())
            {
                var testFile = new TestFile("testFile.cs", "class _this_is_invalid_test_class_name { int n = true; }");

                host.AddFilesToWorkspace(testFile);

                var testAnalyzerRef = new TestAnalyzerReference("TS1234", isEnabledByDefault: true);

                AddProjectWitFile(host, testFile, testAnalyzerRef);

                var result = await host.RequestCodeCheckAsync();
                Assert.Contains(result.QuickFixes, f => f.Text.Contains(testAnalyzerRef.Id.ToString()));
            }
        }

        private OmniSharpTestHost GetHost()
        {
            return OmniSharpTestHost.Create(testOutput: _testOutput,
                configurationData: TestHelpers.GetConfigurationDataWithAnalyzerConfig(roslynAnalyzersEnabled: true));
        }

        [Fact]
        public async Task Always_return_results_from_net_default_analyzers()
        {
            using (var host = GetHost())
            {
                var testFile = new TestFile("testFile_1.cs", "class SomeClass { int n = true; }");

                AddProjectWitFile(host, testFile);

                var result = await host.RequestCodeCheckAsync();

                Assert.Contains(result.QuickFixes.Where(x => x.FileName == testFile.FileName), f => f.Text.Contains("CS"));
            }
        }

        [Fact]
        public async Task Rulesets_should_work_with_syntax_analyzers()
        {
            using (var host = GetHost())
            {
                var testFile = new TestFile("testFile_9.cs", @"
                    class Program
                    {
                        static void Main(string[] args)
                        {
                            return;
                            Console.WriteLine(null); // This is CS0162, unreachable code.
                        }
                    }");
                var ruleService = host.GetExport<RulesetsForProjects>();

                var projectIds = AddProjectWitFile(host, testFile);

                var testRules = CreateRules("CS0162", ReportDiagnostic.Hidden);

                ruleService.AddOrUpdateRuleset(projectIds.Single(), new RuleSet(
                    "",
                    new ReportDiagnostic(),
                    testRules.ToImmutableDictionary(),
                    new ImmutableArray<RuleSetInclude>()));

                var result = await host.RequestCodeCheckAsync();

                Assert.Contains(result.QuickFixes.OfType<DiagnosticLocation>(), f => f.Text.Contains("CS0162") && f.LogLevel == "Hidden");
            }
        }

        [Fact]
        public async Task When_rules_udpate_diagnostic_severity_then_show_them_with_new_severity()
        {
            using (var host = GetHost())
            {
                var testFile = new TestFile("testFile_2.cs", "class _this_is_invalid_test_class_name { int n = true; }");
                var ruleService = host.GetExport<RulesetsForProjects>();

                var testAnalyzerRef = new TestAnalyzerReference("TS1100");

                var projectIds = AddProjectWitFile(host, testFile, testAnalyzerRef);
                var testRules = CreateRules(testAnalyzerRef.Id.ToString(), ReportDiagnostic.Hidden);

                ruleService.AddOrUpdateRuleset(projectIds.Single(), new RuleSet(
                    "",
                    new ReportDiagnostic(),
                    testRules.ToImmutableDictionary(),
                    new ImmutableArray<RuleSetInclude>()));

                var result = await host.RequestCodeCheckAsync("testFile_2.cs");
                Assert.Contains(result.QuickFixes.OfType<DiagnosticLocation>(), f => f.Text.Contains(testAnalyzerRef.Id.ToString()) && f.LogLevel == "Hidden");
            }
        }

        private static Dictionary<string, ReportDiagnostic> CreateRules(string analyzerId, ReportDiagnostic diagnostic)
        {
            return new Dictionary<string, ReportDiagnostic>
            {
                { analyzerId, diagnostic }
            };
        }

        [Fact]
        // This is important because hidden still allows code fixes to execute, not prevents it, for this reason suppressed analytics should not be returned at all.
        public async Task When_custom_rule_is_set_to_none_dont_return_results_at_all()
        {
            using (var host = GetHost())
            {
                var testFile = new TestFile("testFile_3.cs", "class _this_is_invalid_test_class_name { int n = true; }");

                var ruleService = host.GetExport<RulesetsForProjects>();

                var testAnalyzerRef = new TestAnalyzerReference("TS1101");

                var projectIds = AddProjectWitFile(host, testFile, testAnalyzerRef);

                var testRules = CreateRules(testAnalyzerRef.Id.ToString(), ReportDiagnostic.Suppress);

                ruleService.AddOrUpdateRuleset(projectIds.Single(), new RuleSet(
                    "",
                    new ReportDiagnostic(),
                    testRules.ToImmutableDictionary(),
                    new ImmutableArray<RuleSetInclude>()));

                var result = await host.RequestCodeCheckAsync("testFile_3.cs");
                Assert.DoesNotContain(result.QuickFixes, f => f.Text.Contains(testAnalyzerRef.Id.ToString()));
            }
        }

        [Fact]
        public async Task When_diagnostic_is_disabled_by_default_updating_rule_will_enable_it()
        {
            using (var host = GetHost())
            {
                var testFile = new TestFile("testFile_4.cs", "class _this_is_invalid_test_class_name { int n = true; }");
                var ruleService = host.GetExport<RulesetsForProjects>();

                var testAnalyzerRef = new TestAnalyzerReference("TS1101", isEnabledByDefault: false);

                var projectIds = AddProjectWitFile(host, testFile, testAnalyzerRef);

                var testRules = CreateRules(testAnalyzerRef.Id.ToString(), ReportDiagnostic.Error);

                ruleService.AddOrUpdateRuleset(projectIds.Single(), new RuleSet(
                    "",
                    new ReportDiagnostic(),
                    testRules.ToImmutableDictionary(),
                    new ImmutableArray<RuleSetInclude>()));

                var result = await host.RequestCodeCheckAsync("testFile_4.cs");
                Assert.Contains(result.QuickFixes, f => f.Text.Contains(testAnalyzerRef.Id.ToString()));
            }
        }

        private IEnumerable<ProjectId> AddProjectWitFile(OmniSharpTestHost host, TestFile testFile, TestAnalyzerReference testAnalyzerRef = null)
        {
            var analyzerReferences = testAnalyzerRef == null ? default :
                new AnalyzerReference[] { testAnalyzerRef }.ToImmutableArray();

            return TestHelpers.AddProjectToWorkspace(
                            host.Workspace,
                            "project.csproj",
                            new[] { "netcoreapp2.1" },
                            new[] { testFile },
                            analyzerRefs: analyzerReferences);
        }
    }
}
