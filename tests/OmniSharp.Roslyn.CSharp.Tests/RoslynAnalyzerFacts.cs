using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using OmniSharp.Models.CodeCheck;
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
            private readonly Guid _id;

            public TestAnalyzerReference(Guid testAnalyzerId)
            {
                _id = testAnalyzerId;
            }

            public override string FullPath => null;
            public override object Id => _id.ToString();
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
            public TestDiagnosticAnalyzer(string id)
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

            SharedOmniSharpTestHost.AddFilesToWorkspace(testFile);

            var analyzerId = Guid.NewGuid();

            var testAnalyzerRef = new TestAnalyzerReference(analyzerId);

            TestHelpers.AddProjectToWorkspace(
                SharedOmniSharpTestHost.Workspace,
                "project.csproj",
                new[] { "netcoreapp2.1" },
                new[] { testFile },
                analyzerRefs: new AnalyzerReference []{ testAnalyzerRef }.ToImmutableArray());

            var codeCheckService = GetRequestHandler(SharedOmniSharpTestHost);

            await AssertForEventuallyMatch(
                codeCheckService.Handle(new CodeCheckRequest()), x => x.QuickFixes.Any(f => f.Text.Contains(analyzerId.ToString())));
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

            var codeCheckService = GetRequestHandler(SharedOmniSharpTestHost);

            await AssertForEventuallyMatch(
                codeCheckService.Handle(new CodeCheckRequest()), x => x.QuickFixes.Any(f => f.Text.Contains("CS5001")));
        }

        private static async Task<T> AssertForEventuallyMatch<T>(Task<T> func, Predicate<T> check, int retryCount = 50)
        {
            while (retryCount-- > 0)
            {
                var result = await func;

                if (check(result))
                    return result;

                await Task.Delay(200);
            }

            throw new InvalidOperationException("Timeout expired before meaningfull result returned.");
        }
    }
}
