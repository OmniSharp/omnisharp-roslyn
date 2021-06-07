using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class TestSourceGenerator : ISourceGenerator
    {

        public TestSourceGenerator(Action<GeneratorExecutionContext> execute)
        {
            _execute = execute;
        }

        private readonly Action<GeneratorExecutionContext> _execute;

        public void Execute(GeneratorExecutionContext context)
        {
            _execute(context);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }
    }

    public class TestGeneratorReference : AnalyzerReference
    {
        private readonly bool _isEnabledByDefault;
        private readonly Action<GeneratorExecutionContext> _execute;

        public TestGeneratorReference(Action<GeneratorExecutionContext> execute, [CallerMemberName] string testId = "", bool isEnabledByDefault = true)
        {
            Id = testId;
            _isEnabledByDefault = isEnabledByDefault;
            _execute = execute;
        }

        public override ImmutableArray<ISourceGenerator> GetGenerators(string language)
            => ImmutableArray.Create<ISourceGenerator>(new TestSourceGenerator(_execute));

        public override ImmutableArray<ISourceGenerator> GetGeneratorsForAllLanguages()
            => ImmutableArray.Create<ISourceGenerator>(new TestSourceGenerator(_execute));

        public override string FullPath => null;
        public override object Id { get; }
        public override string Display => $"{nameof(TestGeneratorReference)}_{Id}";

        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) => ImmutableArray<DiagnosticAnalyzer>.Empty;
        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() => ImmutableArray<DiagnosticAnalyzer>.Empty;
    }
}
