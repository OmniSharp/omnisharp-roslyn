using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Models.CodeCheck;
using OmniSharp.Models.Diagnostics;
using OmniSharp.Roslyn.CSharp.Services.Diagnostics;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class RoslynAnalyzerFacts : AbstractSingleRequestHandlerTestFixture<CodeCheckService>
    {
        protected override string EndpointName => OmniSharpEndpoints.CodeCheck;

        public class TestAnalyzerReference : AnalyzerReference
        {
            private readonly string _id;

            public TestAnalyzerReference(string testAnalyzerId)
            {
                _id = testAnalyzerId;
            }

            public override string FullPath => null;
            public override object Id => _id;
            public override string Display => $"{nameof(TestAnalyzerReference)}_{Id}";

            public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language)
            {
                return new DiagnosticAnalyzer[] { new TestDiagnosticAnalyzer(Id.ToString()) }.ToImmutableArray();
            }

            public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages()
            {
                return new DiagnosticAnalyzer[] { new TestDiagnosticAnalyzer(Id.ToString()) }.ToImmutableArray();
            }
        }

        [DiagnosticAnalyzer(LanguageNames.CSharp)]
        public class TestDiagnosticAnalyzer : DiagnosticAnalyzer
        {
            public TestDiagnosticAnalyzer(string id, bool suppressed = false)
            {
                this.id = id;
            }

            private DiagnosticDescriptor Rule => new DiagnosticDescriptor(
                this.id,
                "Testtitle",
                "Type name '{0}' contains lowercase letters",
                "Naming",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true
            );

            private readonly string id;

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

        public RoslynAnalyzerFacts(ITestOutputHelper output, SharedOmniSharpHostFixture sharedOmniSharpHostFixture)
            : base(output, sharedOmniSharpHostFixture)
        {
        }

        [Fact]
        public async Task When_custom_analyzers_are_executed_then_return_results()
        {
            var testFile = new TestFile("testFile.cs", "class _this_is_invalid_test_class_name { int n = true; }");
            var codeCheckService = GetRequestHandler(SharedOmniSharpTestHost);

            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);

            var analyzerId = "TS1000".ToString();

            var testAnalyzerRef = new TestAnalyzerReference(analyzerId);

            TestHelpers.AddProjectToWorkspace(
                SharedOmniSharpTestHost.Workspace,
                "project.csproj",
                new[] { "netcoreapp2.1" },
                new[] { testFile },
                analyzerRefs: new AnalyzerReference []{ testAnalyzerRef }.ToImmutableArray());

            var result = await codeCheckService.Handle(new CodeCheckRequest());

            Assert.Contains(result.QuickFixes, f => f.Text.Contains(analyzerId));
        }

        [Fact]
        public async Task Always_return_results_from_net_default_analyzers()
        {
            var testFile = new TestFile("testFile.cs", "class SomeClass { int n = true; }");
            var codeCheckService = GetRequestHandler(SharedOmniSharpTestHost);

            TestHelpers.AddProjectToWorkspace(
                SharedOmniSharpTestHost.Workspace,
                "project.csproj",
                new[] { "netcoreapp2.1" },
                new[] { testFile });

            var result = await codeCheckService.Handle(new CodeCheckRequest());

            Assert.Contains(result.QuickFixes, f => f.Text.Contains("CS0029"));
        }

        [Fact]
        public async Task When_rules_udpate_diagnostic_severity_then_show_them_with_new_severity()
        {
            var testFile = new TestFile("testFile.cs", "class _this_is_invalid_test_class_name { int n = true; }");
            var codeCheckService = GetRequestHandler(SharedOmniSharpTestHost);
            var ruleService = SharedOmniSharpTestHost.GetExport<RulesetsForProjects>();

            const string analyzerId = "TS1100";

            var testAnalyzerRef = new TestAnalyzerReference(analyzerId);

            var projectIds = TestHelpers.AddProjectToWorkspace(
                SharedOmniSharpTestHost.Workspace,
                "project.csproj",
                new[] { "netcoreapp2.1" },
                new[] { testFile },
                analyzerRefs: new AnalyzerReference[] { testAnalyzerRef }.ToImmutableArray());

            var testRules = new Dictionary<string, ReportDiagnostic>
            {
                { analyzerId, ReportDiagnostic.Hidden }
            };

            ruleService.AddOrUpdateRuleset(projectIds.Single(), new RuleSet(
                "",
                new ReportDiagnostic(),
                testRules.ToImmutableDictionary(),
                new ImmutableArray<RuleSetInclude>()));

            var result = await codeCheckService.Handle(new CodeCheckRequest());

            Assert.Contains(result.QuickFixes.OfType<DiagnosticLocation>(), f => f.Text.Contains(analyzerId) && f.LogLevel == "Hidden");
        }

        [Fact]
        // This is important because hidden still allows code fixes to execute, not prevents it, for this reason suppressed analytics should not be returned at all.
        public async Task When_custom_rule_is_set_to_none_dont_return_results_at_all()
        {
            var testFile = new TestFile("testFile.cs", "class _this_is_invalid_test_class_name { int n = true; }");
            var codeCheckService = GetRequestHandler(SharedOmniSharpTestHost);
            var ruleService = SharedOmniSharpTestHost.GetExport<RulesetsForProjects>();

            const string analyzerId = "TS1101";

            var testAnalyzerRef = new TestAnalyzerReference(analyzerId);

            var projectIds = TestHelpers.AddProjectToWorkspace(
                SharedOmniSharpTestHost.Workspace,
                "project.csproj",
                new[] { "netcoreapp2.1" },
                new[] { testFile },
                analyzerRefs: new AnalyzerReference[] { testAnalyzerRef }.ToImmutableArray());

            var testRules = new Dictionary<string, ReportDiagnostic>
            {
                { analyzerId, ReportDiagnostic.Suppress }
            };

            ruleService.AddOrUpdateRuleset(projectIds.Single(), new RuleSet(
                "",
                new ReportDiagnostic(),
                testRules.ToImmutableDictionary(),
                new ImmutableArray<RuleSetInclude>()));

            var result = await codeCheckService.Handle(new CodeCheckRequest());

            Assert.DoesNotContain(result.QuickFixes.OfType<DiagnosticLocation>(), f => f.Text.Contains(analyzerId));
        }

        [Fact]
        public async Task When_diagnostic_is_disabled_by_default_updating_rule_will_enable_it()
        {
            await Task.Delay(1);
            // TODO...
        }
    }
}
